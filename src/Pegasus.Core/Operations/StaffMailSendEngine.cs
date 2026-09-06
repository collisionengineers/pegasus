using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Operations;

public static class StaffMailStatePolicy
{
    public static void RequireTransition(StaffMailState current, StaffMailState next)
    {
        var valid = (current, next) switch
        {
            (StaffMailState.Prepared, StaffMailState.DraftCreating or StaffMailState.Failed or StaffMailState.Unknown or StaffMailState.Cancelled) => true,
            (StaffMailState.DraftCreating, StaffMailState.DraftReady or StaffMailState.Failed or StaffMailState.Unknown) => true,
            (StaffMailState.DraftReady, StaffMailState.DraftReady or StaffMailState.Sending or StaffMailState.Failed or StaffMailState.Unknown or StaffMailState.Cancelled) => true,
            (StaffMailState.Sending, StaffMailState.Submitted or StaffMailState.Sent or StaffMailState.Failed or StaffMailState.Unknown) => true,
            (StaffMailState.Submitted, StaffMailState.Sent) => true,
            (StaffMailState.Unknown, StaffMailState.Sent or StaffMailState.Cancelled) => true,
            _ => false
        };
        if (!valid)
        {
            throw new InvalidOperationException($"Staff mail cannot transition from {current} to {next}.");
        }
    }
}

public sealed record ApprovedStaffSendMailbox(
    Guid Id, string GraphMailboxId, long Generation, long EncodedMessageSizeLimit);

public interface IApprovedStaffSendMailboxQueries
{
    Task<ApprovedStaffSendMailbox?> GetAsync(Guid mailboxId, CancellationToken cancellationToken);
}

public interface IStaffMailEvidenceReconciler
{
    Task ReconcileAsync(Guid approvedMailboxId, CancellationToken cancellationToken);
}

public interface IStaffMailExecutionLock
{
    Task<IAsyncDisposable> AcquireAsync(Guid operationId, CancellationToken cancellationToken);
}

public sealed record StaffMailDraftResult(string ImmutableDraftId);
public sealed record StaffMailDraftLookupResult(
    StaffMailDraftResult? Draft, string? Continuation, bool Complete);
public sealed record StaffMailSubmitResult(DateTimeOffset SubmittedAtUtc);
public sealed record StaffMailAttachmentContent(StaffMailAttachment Attachment, Stream Content);
public sealed class StaffMailTransportRejectedException(string failureCode) : Exception
{
    public string FailureCode { get; } = failureCode;
}
public sealed record StaffMailExecution(
    string ActorSubjectId, StaffMailOperation Operation, string? DraftImmutableId,
    IReadOnlyList<StaffMailAttachment> Attachments, StaffMailPurpose Purpose,
    Guid ContextId, long ContextVersion, Guid? CaseId);

public interface IStaffMailTransport
{
    Task ValidateEncodedSizeAsync(
        ApprovedStaffSendMailbox mailbox, StaffMailOperation operation,
        StaffMailSendCommand command,
        IReadOnlyList<StaffMailAttachmentContent> attachments, CancellationToken cancellationToken);
    Task<StaffMailDraftLookupResult> FindDraftAsync(
        ApprovedStaffSendMailbox mailbox, StaffMailOperation operation,
        CancellationToken cancellationToken);
    Task<StaffMailDraftResult> CreateDraftAsync(
        ApprovedStaffSendMailbox mailbox, StaffMailOperation operation,
        StaffMailSendCommand command,
        CancellationToken cancellationToken);
    Task AttachAsync(
        ApprovedStaffSendMailbox mailbox, Guid operationId, string immutableDraftId,
        StaffMailAttachment attachment, Stream content, CancellationToken cancellationToken);
    Task<StaffMailSubmitResult> SendDraftAsync(
        ApprovedStaffSendMailbox mailbox, string immutableDraftId, CancellationToken cancellationToken);
}

public interface IStaffMailSendStore
{
    Task<StaffMailOperation> PrepareAsync(
        StaffMailSendCommand command, string payloadHash, DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
    Task<StaffMailOperation?> GetAsync(
        string actorSubjectId, Guid operationId, CancellationToken cancellationToken);
    Task<StaffMailExecution?> GetExecutionAsync(
        string actorSubjectId, Guid operationId, CancellationToken cancellationToken);
    Task<StaffMailExecution?> GetExecutionForObservationAsync(
        ActionActor systemActor, Guid operationId, CancellationToken cancellationToken);
    Task RequireCurrentStaffAsync(string actorSubjectId, CancellationToken cancellationToken);
    Task<StaffMailOperation> TransitionAsync(
        string actorSubjectId, Guid operationId, long expectedVersion,
        StaffMailState state, StaffMailAttemptStage? stage, string? draftImmutableId,
        DateTimeOffset? submittedAtUtc, DateTimeOffset? observedSentAtUtc,
        string? failureCode, CancellationToken cancellationToken);
    Task<StaffMailOperation> SetReconciliationContinuationAsync(
        string actorSubjectId, Guid operationId, long expectedVersion,
        string? continuation, CancellationToken cancellationToken);
    Task TransitionObservedSentAsync(
        ActionActor systemActor, Guid operationId, long expectedVersion,
        string immutableMessageId, DateTimeOffset providerSentAtUtc,
        DateTimeOffset observedAtUtc, CancellationToken cancellationToken);
}

public sealed class StaffReportSend(
    IReportSendReadiness readiness,
    StaffMailSend mailSend) : IStaffReportSend
{
    public async Task<StaffMailOperation> SendAsync(
        StaffReportSendCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Mail.Purpose != StaffMailPurpose.CaseReport
            || command.Report.Actor.SubjectId != command.Mail.Actor.SubjectId
            || command.Report.GenerationId != command.Mail.ContextId
            || command.Report.ExpectedGenerationVersion != command.Mail.ExpectedContextVersion
            || !command.Report.Artifacts.SequenceEqual(command.Mail.Attachments))
        {
            throw new ArgumentException("The report readiness and staff mail command do not describe the same frozen send.", nameof(command));
        }
        return await mailSend.SendValidatedAsync(
            command.Mail,
            token => readiness.RequireReadyAsync(command.Report, token),
            cancellationToken);
    }
}

public sealed class StaffMailSend(
    IStaffMailSendStore store,
    IApprovedStaffSendMailboxQueries mailboxes,
    IReadLogicalDocumentVersion contentReader,
    IStaffMailTransport transport,
    TimeProvider timeProvider,
    IStaffMailExecutionLock executionLock,
    IStaffMailEvidenceReconciler? evidenceReconciler = null) : IStaffMailSend
{
    public async Task<StaffMailOperation> SendAsync(
        StaffMailSendCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Purpose == StaffMailPurpose.CaseReport)
        {
            throw new InvalidOperationException("Case-report mail must pass the frozen report-readiness boundary.");
        }
        return await SendValidatedAsync(command, _ => Task.CompletedTask, cancellationToken);
    }

    internal async Task<StaffMailOperation> SendValidatedAsync(
        StaffMailSendCommand command,
        Func<CancellationToken, Task> beforeSubmit,
        CancellationToken cancellationToken)
    {
        Validate(command);
        await store.RequireCurrentStaffAsync(command.Actor.SubjectId, cancellationToken);
        var hash = PayloadHash(command);
        var operation = await store.PrepareAsync(command, hash, timeProvider.GetUtcNow(), cancellationToken);
        await using var heldExecutionLock = await executionLock.AcquireAsync(
            operation.Id, cancellationToken);
        operation = await store.GetAsync(command.Actor.SubjectId, operation.Id, cancellationToken)
            ?? throw new InvalidOperationException("The prepared staff mail operation is unavailable.");
        if (operation.State is StaffMailState.Sending or StaffMailState.Submitted
            or StaffMailState.Sent or StaffMailState.Failed
            or StaffMailState.Unknown or StaffMailState.Cancelled)
        {
            return operation;
        }

        var contents = new List<(StaffMailAttachment Attachment, LogicalDocumentContent Content)>();
        try
        {
            var mailbox = await RequireMailboxAsync(command, cancellationToken);
            var execution = await store.GetExecutionAsync(command.Actor.SubjectId, operation.Id, cancellationToken)
                ?? throw new InvalidOperationException("The prepared staff mail execution is unavailable.");
            var attachmentCaseId = execution.CaseId
                ?? (command.Purpose == StaffMailPurpose.CaseReport
                    ? throw new InvalidOperationException("The report generation is not linked to an exact Case.")
                    : command.ContextId);
            contents = await OpenAttachmentsAsync(command, mailbox, attachmentCaseId, cancellationToken);
            await transport.ValidateEncodedSizeAsync(mailbox, operation, command,
                contents.Select(value => new StaffMailAttachmentContent(
                    value.Attachment, value.Content.Content)).ToArray(), cancellationToken);
            await store.RequireCurrentStaffAsync(command.Actor.SubjectId, cancellationToken);
            _ = await RequireMailboxAsync(command, cancellationToken);
            var mayCreateDraft = operation.State == StaffMailState.Prepared;
            if (mayCreateDraft)
            {
                operation = await store.TransitionAsync(
                    command.Actor.SubjectId, operation.Id, operation.Version,
                    StaffMailState.DraftCreating, StaffMailAttemptStage.CreateDraft,
                    null, null, null, null, cancellationToken);
            }
            var draftResult = await CreateOrRecoverDraftAsync(
                command, operation, mailbox, mayCreateDraft, cancellationToken);
            operation = draftResult.Operation;
            var draft = draftResult.Draft;
            if (draft is null)
            {
                return operation;
            }
            operation = await store.TransitionAsync(
                command.Actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.DraftReady, StaffMailAttemptStage.Attach, draft.ImmutableDraftId,
                null, null, null, cancellationToken);
            foreach (var (attachment, content) in contents)
            {
                await store.RequireCurrentStaffAsync(command.Actor.SubjectId, cancellationToken);
                _ = await RequireMailboxAsync(command, cancellationToken);
                await transport.AttachAsync(
                    mailbox, operation.Id, draft.ImmutableDraftId, attachment, content.Content, cancellationToken);
            }
            RequireStaff(command.Actor);
            _ = await RequireMailboxAsync(command, cancellationToken);
            await beforeSubmit(cancellationToken);
            await store.RequireCurrentStaffAsync(command.Actor.SubjectId, cancellationToken);
            operation = await store.TransitionAsync(
                command.Actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.Sending, StaffMailAttemptStage.Send, draft.ImmutableDraftId,
                null, null, null, cancellationToken);
            var submitted = await transport.SendDraftAsync(mailbox, draft.ImmutableDraftId, cancellationToken);
            return await store.TransitionAsync(
                command.Actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.Submitted, StaffMailAttemptStage.ObserveSent, draft.ImmutableDraftId,
                submitted.SubmittedAtUtc, null, null, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await MarkUnknownAsync(command.Actor.SubjectId, operation, CancellationToken.None);
            throw;
        }
        catch (StaffMailTransportRejectedException exception)
        {
            await MarkFailedAsync(command.Actor.SubjectId, operation, exception.FailureCode, cancellationToken);
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            await MarkFailedAsync(
                command.Actor.SubjectId, operation, "staff_send_authorization_lost", cancellationToken);
            throw;
        }
        catch (InvalidDataException)
        {
            await MarkFailedAsync(command.Actor.SubjectId, operation,
                "staff_send_content_invalid", cancellationToken);
            throw;
        }
        catch
        {
            await MarkUnknownAsync(command.Actor.SubjectId, operation, cancellationToken);
            throw;
        }
        finally
        {
            foreach (var (_, content) in contents)
            {
                await content.DisposeAsync();
            }
        }
    }

    public Task<StaffMailOperation?> GetAsync(
        ActionActor actor, Guid operationId, CancellationToken cancellationToken)
    {
        RequireStaff(actor);
        return store.GetAsync(actor.SubjectId, operationId, cancellationToken);
    }

    public async Task<StaffMailOperation> ReconcileAsync(
        ActionActor actor, Guid operationId, long expectedVersion,
        CancellationToken cancellationToken)
    {
        RequireStaff(actor);
        var execution = await store.GetExecutionAsync(actor.SubjectId, operationId, cancellationToken)
            ?? throw new KeyNotFoundException("The staff mail operation was not found.");
        if (execution.Operation.Version != expectedVersion)
        {
            throw new InvalidOperationException("The staff mail operation changed concurrently.");
        }
        if (execution.Operation.State == StaffMailState.Unknown
            && execution.Operation.AttemptStage == StaffMailAttemptStage.CreateDraft)
        {
            await store.RequireCurrentStaffAsync(actor.SubjectId, cancellationToken);
            var mailbox = await mailboxes.GetAsync(
                execution.Operation.ApprovedMailboxId, cancellationToken)
                ?? throw new InvalidOperationException("The approved staff mailbox is unavailable.");
            if (mailbox.Generation != execution.Operation.MailboxGeneration)
                throw new InvalidOperationException("The approved staff mailbox generation changed.");
            var lookup = await transport.FindDraftAsync(mailbox, execution.Operation, cancellationToken);
            var operation = execution.Operation;
            if (!string.Equals(operation.ReconciliationContinuation, lookup.Continuation,
                    StringComparison.Ordinal))
            {
                operation = await store.SetReconciliationContinuationAsync(
                    actor.SubjectId, operation.Id, operation.Version,
                    lookup.Continuation, cancellationToken);
            }
            if (!lookup.Complete || lookup.Draft is null)
                return operation;
            return await store.TransitionAsync(
                actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.DraftReady, StaffMailAttemptStage.Attach,
                lookup.Draft.ImmutableDraftId, null, null, null, cancellationToken);
        }
        if (execution.Operation.State is StaffMailState.Sending or StaffMailState.Submitted or StaffMailState.Unknown)
        {
            if (evidenceReconciler is null)
            {
                throw new InvalidOperationException("Retained Sent-evidence reconciliation is unavailable.");
            }
            await evidenceReconciler.ReconcileAsync(
                execution.Operation.ApprovedMailboxId, cancellationToken);
        }
        return (await store.GetExecutionAsync(actor.SubjectId, operationId, cancellationToken)
            ?? throw new KeyNotFoundException("The staff mail operation was not found.")).Operation;
    }

    public async Task<StaffMailOperation> CancelAsync(
        ActionActor actor, Guid operationId, long expectedVersion,
        CancellationToken cancellationToken)
    {
        RequireStaff(actor);
        var operation = await store.GetAsync(actor.SubjectId, operationId, cancellationToken)
            ?? throw new KeyNotFoundException("The staff mail operation was not found.");
        if (operation.State is StaffMailState.Sending or StaffMailState.Submitted or StaffMailState.Sent)
        {
            throw new InvalidOperationException("A submitted staff mail operation cannot be cancelled.");
        }
        return await store.TransitionAsync(
            actor.SubjectId, operationId, expectedVersion, StaffMailState.Cancelled,
            operation.AttemptStage, null, operation.SubmittedAtUtc,
            operation.ObservedSentAtUtc, null, cancellationToken);
    }

    private async Task<(StaffMailOperation Operation, StaffMailDraftResult? Draft)> CreateOrRecoverDraftAsync(
        StaffMailSendCommand command, StaffMailOperation operation,
        ApprovedStaffSendMailbox mailbox, bool mayCreate, CancellationToken cancellationToken)
    {
        var lookup = await transport.FindDraftAsync(mailbox, operation, cancellationToken);
        if (!string.Equals(operation.ReconciliationContinuation, lookup.Continuation,
                StringComparison.Ordinal))
        {
            operation = await store.SetReconciliationContinuationAsync(
                command.Actor.SubjectId, operation.Id, operation.Version,
                lookup.Continuation, cancellationToken);
        }
        if (!lookup.Complete)
        {
            operation = await store.TransitionAsync(
                command.Actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.Unknown, StaffMailAttemptStage.CreateDraft,
                null, null, null, "staff_mail_draft_lookup_incomplete", cancellationToken);
            return (operation, null);
        }
        if (lookup.Draft is not null)
        {
            return (operation, lookup.Draft);
        }
        if (operation.State == StaffMailState.DraftReady)
        {
            throw new InvalidOperationException("The recorded draft is unavailable for reconciliation.");
        }
        if (!mayCreate && operation.State == StaffMailState.DraftCreating)
        {
            throw new InvalidOperationException(
                "Draft creation could not be reconciled; a replacement draft was not created.");
        }
        return (operation, await transport.CreateDraftAsync(
            mailbox, operation, command, cancellationToken));
    }

    private async Task<ApprovedStaffSendMailbox> RequireMailboxAsync(
        StaffMailSendCommand command, CancellationToken cancellationToken)
    {
        var mailbox = await mailboxes.GetAsync(command.ApprovedMailboxId, cancellationToken)
            ?? throw new UnauthorizedAccessException("The approved mailbox cannot send staff mail.");
        if (mailbox.Generation != command.ExpectedMailboxGeneration)
        {
            throw new UnauthorizedAccessException("The approved mailbox generation changed.");
        }
        return mailbox;
    }

    private async Task<List<(StaffMailAttachment Attachment, LogicalDocumentContent Content)>>
        OpenAttachmentsAsync(StaffMailSendCommand command, ApprovedStaffSendMailbox mailbox,
            Guid caseId,
            CancellationToken cancellationToken)
    {
        var result = new List<(StaffMailAttachment, LogicalDocumentContent)>();
        try
        {
            foreach (var attachment in command.Attachments)
            {
                result.Add((attachment, await contentReader.OpenAsync(
                    new(command.Actor, attachment.DocumentId, attachment.VersionId, null,
                        caseId, null, attachment.Sha256, attachment.ContentLength),
                    cancellationToken)));
            }
            return result;
        }
        catch
        {
            foreach (var (_, content) in result)
            {
                await content.DisposeAsync();
            }
            throw;
        }
    }

    private async Task MarkUnknownAsync(
        string actorSubjectId, StaffMailOperation operation, CancellationToken cancellationToken)
    {
        await store.TransitionAsync(
            actorSubjectId, operation.Id, operation.Version, StaffMailState.Unknown,
            operation.AttemptStage, null, operation.SubmittedAtUtc,
            operation.ObservedSentAtUtc, "provider_outcome_unknown", cancellationToken);
    }

    private async Task MarkFailedAsync(
        string actorSubjectId, StaffMailOperation operation, string failureCode,
        CancellationToken cancellationToken)
    {
        await store.TransitionAsync(
            actorSubjectId, operation.Id, operation.Version, StaffMailState.Failed,
            operation.AttemptStage, null, operation.SubmittedAtUtc,
            operation.ObservedSentAtUtc, failureCode, cancellationToken);
    }

    private static void Validate(StaffMailSendCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        RequireStaff(command.Actor);
        if (command.ApprovedMailboxId == Guid.Empty || command.ExpectedMailboxGeneration <= 0
            || command.ContextId == Guid.Empty || command.ExpectedContextVersion <= 0
            || string.IsNullOrWhiteSpace(command.OperationKey) || command.OperationKey.Length > 100
            || string.IsNullOrWhiteSpace(command.Subject) || command.Subject.Length > 998
            || command.Body is null
            || (string.IsNullOrWhiteSpace(command.Body) && command.Attachments.Count == 0)
            || command.To.Count + command.Cc.Count == 0
            || command.Attachments.Any(value => value.DocumentId == Guid.Empty
                || value.VersionId == Guid.Empty || value.ContentLength <= 0
                || value.Sha256.Length != 64 || string.IsNullOrWhiteSpace(value.FileName)))
        {
            throw new ArgumentException("The staff mail command is invalid.", nameof(command));
        }
        if (command.ComposeMode == StaffMailComposeMode.New ^ command.OriginalMessage is null)
        {
            throw new ArgumentException("Reply, reply-all and forward require an original message; new mail must not name one.", nameof(command));
        }
    }

    private static void RequireStaff(ActionActor actor)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (actor.Kind != ActorKind.Staff)
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
    }

    private static string PayloadHash(StaffMailSendCommand command)
    {
        var json = JsonSerializer.Serialize(command, JsonSerializerOptions.Web);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
