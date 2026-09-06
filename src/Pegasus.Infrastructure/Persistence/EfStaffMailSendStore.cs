using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfStaffMailSendStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IStaffMailSendStore, IApprovedStaffSendMailboxQueries
{
    public async Task<ApprovedStaffSendMailbox?> GetAsync(
        Guid mailboxId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var approved = ApprovedMailboxState.Approved.ToString();
        return await db.ApprovedMailboxes.AsNoTracking()
            .Where(value => value.Id == mailboxId
                && value.State == approved
                && value.AllowStaffSend
                && value.ActivatedAtUtc != null
                && value.MailboxIdentity != null
                && value.MailboxGeneration > 0
                && value.VerifiedEncodedMessageSizeLimit != null)
            .Select(value => new ApprovedStaffSendMailbox(
                value.Id,
                value.MailboxIdentity!,
                value.MailboxGeneration,
                value.VerifiedEncodedMessageSizeLimit!.Value))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<StaffMailOperation> PrepareAsync(
        StaffMailSendCommand command, string payloadHash, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        if (command.OriginalMessage is { } original)
        {
            await AcquireOriginalMessagePrepareLockAsync(
                db, transaction, command.ApprovedMailboxId, original.RetainedMessageId,
                cancellationToken);
            var retained = await db.Set<RetainedMailboxMessageEntity>().AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == original.RetainedMessageId
                    && value.MailboxId == command.ApprovedMailboxId,
                    cancellationToken)
                ?? throw new UnauthorizedAccessException(
                    "The original retained message is not owned by the approved mailbox.");
            if (original.ApprovedMailboxId != command.ApprovedMailboxId
                || !string.Equals(retained.ImmutableMessageId, original.ImmutableMessageId, StringComparison.Ordinal)
                || !string.Equals(retained.InternetMessageIdentity, original.InternetMessageId, StringComparison.Ordinal)
                || !string.Equals(retained.ConversationIdentity, original.ConversationId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "The original retained message identity does not match durable mail evidence.");
            }
        }
        var rows = db.Set<StaffMailSendOperationEntity>();
        var existing = await rows.SingleOrDefaultAsync(value =>
            value.ActorSubjectId == command.Actor.SubjectId
            && value.MailboxId == command.ApprovedMailboxId
            && value.OperationKey == command.OperationKey,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The staff mail operation key was already used for different content.");
            }
            return Map(existing);
        }

        if (command.OriginalMessage is { } activeOriginal
            && await rows.AnyAsync(value =>
                value.MailboxId == command.ApprovedMailboxId
                && value.OriginalRetainedMessageId == activeOriginal.RetainedMessageId
                && value.State != StaffMailState.Sent
                && value.State != StaffMailState.Failed
                && value.State != StaffMailState.Cancelled,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The retained message already has an active staff mail operation.");
        }

        var entity = new StaffMailSendOperationEntity
        {
            Id = Guid.NewGuid(),
            ActorSubjectId = command.Actor.SubjectId,
            MailboxId = command.ApprovedMailboxId,
            MailboxGeneration = command.ExpectedMailboxGeneration,
            OperationKey = command.OperationKey,
            PayloadHash = payloadHash,
            Purpose = command.Purpose,
            ContextId = command.ContextId,
            ContextVersion = command.ExpectedContextVersion,
            ComposeMode = command.ComposeMode,
            OriginalRetainedMessageId = command.OriginalMessage?.RetainedMessageId,
            OriginalImmutableMessageId = command.OriginalMessage?.ImmutableMessageId,
            OriginalInternetMessageId = command.OriginalMessage?.InternetMessageId,
            OriginalConversationId = command.OriginalMessage?.ConversationId,
            RecipientsJson = JsonSerializer.Serialize(new Recipients(command.To, command.Cc)),
            Subject = command.Subject,
            Body = command.Body,
            AttachmentsJson = JsonSerializer.Serialize(command.Attachments),
            State = StaffMailState.Prepared,
            CorrelationMarker = $"x-pegasus-operation-id:{Guid.NewGuid():D}",
            CreatedAtUtc = nowUtc,
            RequestedAtUtc = nowUtc,
            Version = 1,
            ConcurrencyToken = Guid.NewGuid()
        };
        // The marker names the durable operation, not a separate random identity.
        entity.CorrelationMarker = $"x-pegasus-operation-id:{entity.Id:D}";
        rows.Add(entity);
        db.ActionHistory.Add(History(entity, command.Actor.Kind, command.Actor.SubjectId,
            command.Actor.Roles.Select(value => value.ToString()), "staff-mail-prepared", nowUtc,
            command.OperationKey, null, null, Map(entity)));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    private static async Task AcquireOriginalMessagePrepareLockAsync(
        PegasusDbContext db,
        IDbContextTransaction transaction,
        Guid mailboxId,
        Guid retainedMessageId,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandTimeout = db.Database.GetCommandTimeout() ?? command.CommandTimeout;
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = @lockTimeout;
            SELECT @result;
            """;
        AddParameter(command, "@resource",
            $"staff-mail-original:{mailboxId:N}:{retainedMessageId:N}");
        AddParameter(command, "@lockTimeout", checked(command.CommandTimeout * 1000));
        var result = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        if (result < 0)
        {
            throw new InvalidOperationException(
                "The retained message send could not be serialized.");
        }
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    public async Task<StaffMailOperation?> GetAsync(
        string actorSubjectId, Guid operationId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == operationId
                && value.ActorSubjectId == actorSubjectId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<StaffMailOperation?> GetLatestForOriginalAsync(
        string actorSubjectId, Guid retainedMessageId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .Where(value => value.ActorSubjectId == actorSubjectId
                && value.OriginalRetainedMessageId == retainedMessageId)
            .OrderBy(value => value.State == StaffMailState.Sent
                || value.State == StaffMailState.Failed
                || value.State == StaffMailState.Cancelled)
            .ThenByDescending(value => value.CreatedAtUtc)
            .ThenByDescending(value => value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<StaffMailExecution?> GetExecutionAsync(
        string actorSubjectId, Guid operationId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == operationId
                && value.ActorSubjectId == actorSubjectId, cancellationToken);
        if (entity is null)
        {
            return null;
        }
        var attachments = JsonSerializer.Deserialize<StaffMailAttachment[]>(entity.AttachmentsJson)
            ?? throw new InvalidDataException("The frozen staff mail attachments are invalid.");
        return await MapExecutionAsync(
            db, entity, attachments, requireFrozenGenerationVersion: true, cancellationToken);
    }

    public async Task<StaffMailExecution?> GetExecutionForObservationAsync(
        ActionActor systemActor, Guid operationId, CancellationToken cancellationToken)
    {
        RequireSystemWorker(systemActor);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == operationId, cancellationToken);
        if (entity is null)
        {
            return null;
        }
        var attachments = JsonSerializer.Deserialize<StaffMailAttachment[]>(entity.AttachmentsJson)
            ?? throw new InvalidDataException("The frozen staff mail attachments are invalid.");
        return await MapExecutionAsync(
            db, entity, attachments, requireFrozenGenerationVersion: false, cancellationToken);
    }

    public async Task RequireCurrentStaffAsync(
        string actorSubjectId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(actorSubjectId, out var staffId)
            || staffId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The staff account identity is invalid.");
        }
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var authorizedRoleNames = new[]
        {
            StaffRoleNames.User.ToUpperInvariant(),
            StaffRoleNames.Engineer.ToUpperInvariant(),
            StaffRoleNames.Administrator.ToUpperInvariant()
        };
        var authorized = await db.Users.AsNoTracking()
            .Where(value => value.Id == staffId && value.IsEnabled)
            .AnyAsync(user => db.UserRoles
                .Where(userRole => userRole.UserId == user.Id)
                .Join(db.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (_, role) => role.NormalizedName)
                .Any(roleName => authorizedRoleNames.Contains(roleName!)), cancellationToken);
        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "The staff account is no longer enabled or authorized for casework.");
        }
    }

    public async Task<StaffMailOperation> TransitionAsync(
        string actorSubjectId, Guid operationId, long expectedVersion,
        StaffMailState state, StaffMailAttemptStage? stage, string? draftImmutableId,
        DateTimeOffset? submittedAtUtc, DateTimeOffset? observedSentAtUtc,
        string? failureCode, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<StaffMailSendOperationEntity>().SingleOrDefaultAsync(
            value => value.Id == operationId && value.ActorSubjectId == actorSubjectId,
            cancellationToken) ?? throw new KeyNotFoundException("The staff mail operation was not found.");
        if (entity.Version != expectedVersion)
        {
            throw new InvalidOperationException("The staff mail operation changed concurrently.");
        }
        StaffMailStatePolicy.RequireTransition(entity.State, state);
        var before = Map(entity);
        entity.State = state;
        entity.AttemptStage = stage;
        entity.DraftImmutableId = draftImmutableId ?? entity.DraftImmutableId;
        DateTimeOffset occurredAtUtc;
        if (state != StaffMailState.Unknown || stage != StaffMailAttemptStage.CreateDraft)
        {
            occurredAtUtc = DateTimeOffset.UtcNow;
            entity.LastAttemptAtUtc = occurredAtUtc;
        }
        else
        {
            occurredAtUtc = entity.LastAttemptAtUtc
                ?? throw new InvalidOperationException("The draft creation attempt time is unavailable.");
        }
        entity.SubmittedAtUtc = submittedAtUtc ?? entity.SubmittedAtUtc;
        entity.ObservedSentAtUtc = observedSentAtUtc ?? entity.ObservedSentAtUtc;
        entity.LastError = failureCode;
        entity.Version = checked(entity.Version + 1);
        entity.ConcurrencyToken = Guid.NewGuid();
        var currentRoles = await CurrentRoleNamesAsync(db, actorSubjectId, cancellationToken);
        db.ActionHistory.Add(History(entity, ActorKind.Staff, actorSubjectId, currentRoles,
            $"staff-mail-{state.ToString().ToLowerInvariant()}", occurredAtUtc,
            entity.OperationKey, failureCode, before, Map(entity)));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException(
                "The staff mail operation changed concurrently.", exception);
        }
        return Map(entity);
    }

    public async Task<StaffMailOperation> SetReconciliationContinuationAsync(
        string actorSubjectId, Guid operationId, long expectedVersion,
        string? continuation, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<StaffMailSendOperationEntity>().SingleOrDefaultAsync(
            value => value.Id == operationId && value.ActorSubjectId == actorSubjectId,
            cancellationToken) ?? throw new KeyNotFoundException("The staff mail operation was not found.");
        if (entity.Version != expectedVersion)
            throw new InvalidOperationException("The staff mail operation changed concurrently.");
        entity.ReconciliationContinuation = continuation;
        entity.Version = checked(entity.Version + 1);
        entity.ConcurrencyToken = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task TransitionObservedSentAsync(
        ActionActor systemActor, Guid operationId, long expectedVersion,
        string immutableMessageId, DateTimeOffset providerSentAtUtc,
        DateTimeOffset observedAtUtc, CancellationToken cancellationToken)
    {
        RequireSystemWorker(systemActor);
        if (string.IsNullOrWhiteSpace(immutableMessageId)
            || providerSentAtUtc.Offset != TimeSpan.Zero
            || observedAtUtc.Offset != TimeSpan.Zero
            || providerSentAtUtc > observedAtUtc)
        {
            throw new ArgumentException("The retained Sent observation is invalid.");
        }
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<StaffMailSendOperationEntity>().SingleOrDefaultAsync(
            value => value.Id == operationId, cancellationToken)
            ?? throw new KeyNotFoundException("The staff mail operation was not found.");
        if (entity.Version != expectedVersion)
        {
            throw new InvalidOperationException("The staff mail operation changed concurrently.");
        }
        StaffMailStatePolicy.RequireTransition(entity.State, StaffMailState.Sent);
        entity.State = StaffMailState.Sent;
        entity.AttemptStage = StaffMailAttemptStage.ObserveSent;
        entity.ObservedSentAtUtc = observedAtUtc;
        entity.LastAttemptAtUtc = observedAtUtc;
        entity.LastError = null;
        entity.Version = checked(entity.Version + 1);
        entity.ConcurrencyToken = Guid.NewGuid();
        db.ActionHistory.Add(History(entity, systemActor.Kind, systemActor.SubjectId,
            systemActor.Roles.Select(value => value.ToString()), "staff-mail-sent-observed",
            observedAtUtc, entity.OperationKey, null, null, Map(entity)));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ActionHistoryEntity History(
        StaffMailSendOperationEntity entity, ActorKind actorKind, string actorSubjectId,
        IEnumerable<string> actorRoles, string eventKind,
        DateTimeOffset occurredAtUtc, string correlationId, string? reason,
        StaffMailOperation? before, StaffMailOperation after) => new()
    {
        Id = Guid.NewGuid(), AggregateType = "StaffMailSend",
        AggregateId = entity.Id.ToString("D"), EventKind = eventKind,
        ActorKind = actorKind.ToString(), ActorSubjectId = actorSubjectId,
        ActorRolesJson = JsonSerializer.Serialize(actorRoles.OrderBy(value => value)),
        OccurredAtUtc = occurredAtUtc, Outcome = "succeeded", CorrelationId = correlationId,
        Reason = reason, BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
        AfterJson = JsonSerializer.Serialize(after),
        PolicyVersion = $"staff-mail/{entity.Id:D}/v{entity.Version}"
    };

    private static async Task<string[]> CurrentRoleNamesAsync(
        PegasusDbContext db, string actorSubjectId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(actorSubjectId, out var staffId))
        {
            return [];
        }
        return await db.UserRoles.Where(value => value.UserId == staffId)
            .Join(db.Roles, value => value.RoleId, value => value.Id,
                (_, role) => role.Name ?? role.NormalizedName ?? string.Empty)
            .Where(value => value != string.Empty)
            .ToArrayAsync(cancellationToken);
    }

    private static async Task<StaffMailExecution> MapExecutionAsync(
        PegasusDbContext db, StaffMailSendOperationEntity entity,
        IReadOnlyList<StaffMailAttachment> attachments,
        bool requireFrozenGenerationVersion,
        CancellationToken cancellationToken)
    {
        Guid? caseId = null;
        if (entity.Purpose == StaffMailPurpose.CaseReport)
        {
            caseId = await db.Set<CaseReportGenerationEntity>().AsNoTracking()
                .Where(value => value.Id == entity.ContextId
                    && (!requireFrozenGenerationVersion || value.Version == entity.ContextVersion))
                .Select(value => (Guid?)value.CaseId)
                .SingleOrDefaultAsync(cancellationToken);
            if (caseId is not null)
            {
                var generated = await db.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                    .Where(value => value.GenerationId == entity.ContextId
                        && value.VersionId != null && value.Sha256 != null)
                    .Select(value => new { VersionId = value.VersionId!.Value, Sha256 = value.Sha256! })
                    .ToArrayAsync(cancellationToken);
                var frozen = attachments
                    .Select(value => (value.VersionId, value.Sha256))
                    .OrderBy(value => value.VersionId)
                    .ThenBy(value => value.Sha256, StringComparer.Ordinal)
                    .ToArray();
                var actual = generated
                    .Select(value => (value.VersionId, value.Sha256))
                    .OrderBy(value => value.VersionId)
                    .ThenBy(value => value.Sha256, StringComparer.Ordinal)
                    .ToArray();
                if (!frozen.SequenceEqual(actual))
                {
                    caseId = null;
                }
            }
        }
        return new(
            entity.ActorSubjectId, Map(entity), entity.DraftImmutableId, attachments,
            entity.Purpose, entity.ContextId, entity.ContextVersion, caseId);
    }

    private static void RequireSystemWorker(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != ActorKind.SystemWorker
            || !StaffAuthorization.IsAuthorized(actor, StaffAccessRight.ExecuteSystemWork))
        {
            throw new UnauthorizedAccessException("Sent observation requires a system-worker actor.");
        }
    }

    private static StaffMailOperation Map(StaffMailSendOperationEntity value) => new(
        value.Id, value.State, value.AttemptStage, value.Version,
        value.CreatedAtUtc, value.SubmittedAtUtc, value.ObservedSentAtUtc,
        value.LastError, value.MailboxId, value.MailboxGeneration, value.PayloadHash,
        value.LastAttemptAtUtc, value.UploadSessionExpiresAtUtc,
        value.ReconciliationContinuation, value.DraftImmutableId);

    private sealed record Recipients(
        IReadOnlyList<StaffMailRecipient> To,
        IReadOnlyList<StaffMailRecipient> Cc);
}
