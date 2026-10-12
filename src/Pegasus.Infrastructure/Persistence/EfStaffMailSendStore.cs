using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfStaffMailSendStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IStaffMailSendStore, IApprovedStaffSendMailboxQueries
{
    private static readonly JsonSerializerOptions ReportSnapshotJson = new(JsonSerializerDefaults.Web);

    public async Task<ApprovedStaffSendMailbox?> GetAsync(
        Guid mailboxId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var approved = ApprovedMailboxState.Approved.ToString();
        return await db.ApprovedMailboxes.AsNoTracking()
            .Where(value => value.Id == mailboxId
                && value.State == approved
                && value.AllowOutgoing
                && value.ActivatedAtUtc != null
                && value.MailboxIdentity != null
                && value.SentFolderIdentity != null
                && value.MailboxGeneration > 0)
            .Select(value => new ApprovedStaffSendMailbox(
                value.Id,
                value.MailboxIdentity!,
                value.MailboxGeneration,
                ApprovedStaffSendMailbox.OutgoingEncodedMessageSizeLimit))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<StaffMailOperation> PrepareAsync(
        StaffMailSendCommand command, string payloadHash, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        // Serializable missing-key reads can lock the same index gap even for
        // unrelated messages. Order these short preparation transactions before
        // any reads; the lock is released before attachment or provider work.
        await AcquireTransactionLockAsync(db, transaction, "staff-mail-prepare", cancellationToken);
        if (command.OriginalMessage is { } original)
        {
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

        // The marker names the durable operation, not a separate random identity.
        var operationId = Guid.NewGuid();
        var entity = new StaffMailSendOperationEntity
        {
            Id = operationId,
            ActorKind = command.Actor.Kind,
            ActorSubjectId = command.Actor.SubjectId,
            MailboxId = command.ApprovedMailboxId,
            MailboxGeneration = command.ExpectedMailboxGeneration,
            OperationKey = command.OperationKey,
            PayloadHash = payloadHash,
            Purpose = command.Purpose,
            ContextId = command.ContextId,
            ContextVersion = command.ExpectedContextVersion,
            SecondContextId = command.SecondReport?.ContextId,
            SecondContextVersion = command.SecondReport?.ExpectedContextVersion,
            ComposeMode = command.ComposeMode,
            OriginalRetainedMessageId = command.OriginalMessage?.RetainedMessageId,
            OriginalImmutableMessageId = command.OriginalMessage?.ImmutableMessageId,
            OriginalInternetMessageId = command.OriginalMessage?.InternetMessageId,
            OriginalConversationId = command.OriginalMessage?.ConversationId,
            ResolvesCaseQueries = command.ResolvesCaseQueries,
            RecipientsJson = JsonSerializer.Serialize(new Recipients(command.To, command.Cc)),
            Subject = command.Subject,
            Body = command.Body,
            AttachmentsJson = JsonSerializer.Serialize(command.Attachments),
            ReportDispatchJson = command.ReportDispatch is { } dispatch
                ? JsonSerializer.Serialize(dispatch, ReportSnapshotJson)
                : null,
            State = StaffMailState.Prepared,
            CorrelationMarker = $"x-pegasus-operation-id:{operationId:D}",
            CreatedAtUtc = nowUtc,
            RequestedAtUtc = nowUtc,
            Version = 1,
            ConcurrencyToken = Guid.NewGuid()
        };
        rows.Add(entity);
        db.ActionHistory.Add(History(entity, command.Actor.Kind, command.Actor.SubjectId,
            command.Actor.Roles.Select(value => value.ToString()), "staff-mail-prepared", nowUtc,
            command.OperationKey, null, null, Map(entity)));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    /// <summary>
    /// The roles the staff member held when they prepared the send, as its
    /// <c>staff-mail-prepared</c> history row recorded them. Work the Worker
    /// does in that person's name carries these.
    /// </summary>
    internal static async Task<HashSet<StaffRole>> PreparedRolesAsync(
        PegasusDbContext db, Guid operationId, CancellationToken cancellationToken)
    {
        var aggregateId = operationId.ToString("D");
        var preparedRoles = await db.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == "StaffMailSend"
                && item.AggregateId == aggregateId
                && item.EventKind == "staff-mail-prepared")
            .Select(item => item.ActorRolesJson)
            .FirstOrDefaultAsync(cancellationToken);
        return (preparedRoles is null ? [] : JsonSerializer.Deserialize<string[]>(preparedRoles) ?? [])
            .Select(name => Enum.TryParse<StaffRole>(name, ignoreCase: false, out var role) ? (StaffRole?)role : null)
            .OfType<StaffRole>()
            .ToHashSet();
    }

    private static async Task AcquireTransactionLockAsync(
        PegasusDbContext db,
        IDbContextTransaction transaction,
        string resource,
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
        AddParameter(command, "@resource", resource);
        AddParameter(command, "@lockTimeout", checked(command.CommandTimeout * 1000));
        var result = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        if (result < 0)
        {
            throw new InvalidOperationException(
                "The staff mail preparation could not be serialized.");
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
            // A report reply to the same message is the Case's own send, not
            // this page's correspondence.
            .Where(value => value.ActorSubjectId == actorSubjectId
                && value.OriginalRetainedMessageId == retainedMessageId
                && value.Purpose != StaffMailPurpose.CaseReport)
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
        // The sender recorded at preparation: a member of staff, whose current
        // roles are read, or the Automation Actor, which holds none.
        var currentRoles = entity.ActorKind == ActorKind.Staff
            ? await CurrentRoleNamesAsync(db, actorSubjectId, cancellationToken)
            : Array.Empty<string>();
        db.ActionHistory.Add(History(entity, entity.ActorKind, actorSubjectId, currentRoles,
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
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await AcquireTransactionLockAsync(
            db, transaction, $"staff-mail-observed:{operationId:N}", cancellationToken);
        var entity = await db.Set<StaffMailSendOperationEntity>().SingleOrDefaultAsync(
            value => value.Id == operationId, cancellationToken)
            ?? throw new KeyNotFoundException("The staff mail operation was not found.");
        if (entity.State == StaffMailState.Sent)
        {
            if (entity.ObservedSentImmutableMessageId == immutableMessageId
                && entity.ProviderSentAtUtc == providerSentAtUtc)
            {
                return;
            }

            throw new InvalidOperationException("The staff mail operation has different Sent evidence.");
        }
        if (entity.Version != expectedVersion)
        {
            throw new InvalidOperationException("The staff mail operation changed concurrently.");
        }
        StaffMailStatePolicy.RequireTransition(entity.State, StaffMailState.Sent);
        entity.State = StaffMailState.Sent;
        entity.AttemptStage = StaffMailAttemptStage.ObserveSent;
        entity.ObservedSentImmutableMessageId = immutableMessageId;
        entity.ProviderSentAtUtc = providerSentAtUtc;
        entity.ObservedSentAtUtc = observedAtUtc;
        entity.LastAttemptAtUtc = observedAtUtc;
        entity.LastError = null;
        entity.Version = checked(entity.Version + 1);
        entity.ConcurrencyToken = Guid.NewGuid();
        db.ActionHistory.Add(History(entity, systemActor.Kind, systemActor.SubjectId,
            systemActor.Roles.Select(value => value.ToString()), "staff-mail-sent-observed",
            observedAtUtc, entity.OperationKey, null, null, Map(entity)));
        await FreezeSentReportSpecificationAsync(db, entity, systemActor, observedAtUtc, cancellationToken);
        await CaseQueryLedger.ResolveForObservedReplyAsync(db, entity, observedAtUtc, cancellationToken);
        await RecordCorrespondenceSentAsync(db, entity, cancellationToken);
        await RecordSentChaserAsync(db, entity, providerSentAtUtc, observedAtUtc, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// The Case's Notes line for a general correspondence send, written once
    /// the Sent item is observed (FRD-16 Notes, FRD-21): the sender (a member
    /// of staff or the Automation Actor, as the send recorded it), the
    /// provider's sent time and the subject. The body is never history. A
    /// report send already records its evidence link; a Triage send keeps
    /// Triage history. The event carries the Case's current version without
    /// bumping it, as an operator note does.
    /// </summary>
    private static async Task RecordCorrespondenceSentAsync(
        PegasusDbContext db,
        StaffMailSendOperationEntity mail,
        CancellationToken cancellationToken)
    {
        if (mail.Purpose != StaffMailPurpose.GeneralCorrespondence
            || mail.ProviderSentAtUtc is not { } sentAtUtc)
        {
            return;
        }
        var workflow = await db.CaseWorkflows.SingleOrDefaultAsync(
            item => item.CaseId == mail.ContextId, cancellationToken);
        if (workflow is null)
        {
            return;
        }
        var operationKey = $"correspondence-sent:{mail.Id:N}";
        if (await db.CaseWorkflowEvents.AnyAsync(
                item => item.CaseId == workflow.CaseId && item.OperationKey == operationKey,
                cancellationToken))
        {
            return;
        }
        var roles = await db.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == "StaffMailSend"
                && item.AggregateId == mail.Id.ToString("D")
                && item.EventKind == "staff-mail-prepared")
            .Select(item => item.ActorRolesJson)
            .FirstOrDefaultAsync(cancellationToken);
        db.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = CorrespondenceSentEventType,
            OperationKey = operationKey,
            RequestHash = mail.PayloadHash,
            ActorKind = mail.ActorKind.ToString(),
            ActorSubjectId = mail.ActorSubjectId,
            ActorRolesJson = roles ?? "[]",
            Reason = mail.Subject,
            OccurredAtUtc = sentAtUtc,
            BeforeVersion = workflow.Version,
            AfterVersion = workflow.Version,
            ResultJson = JsonSerializer.Serialize(new CorrespondenceSentValue(
                mail.Id, mail.ObservedSentImmutableMessageId, sentAtUtc, mail.ObservedSentAtUtc))
        });
    }

    /// <summary>The Notes event a general correspondence send leaves once its Sent item is observed.</summary>
    internal const string CorrespondenceSentEventType = "correspondence_sent";

    private sealed record CorrespondenceSentValue(
        Guid OperationId,
        string? SentImmutableMessageId,
        DateTimeOffset ProviderSentAtUtc,
        DateTimeOffset? ObservedAtUtc);

    private static async Task FreezeSentReportSpecificationAsync(
        PegasusDbContext db,
        StaffMailSendOperationEntity mail,
        ActionActor actor,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        if (mail.Purpose != StaffMailPurpose.CaseReport)
        {
            return;
        }

        // Each report the send carried freezes its own work's specification.
        await FreezeSentReportSpecificationAsync(db, mail, mail.ContextId, actor, observedAtUtc, cancellationToken);
        if (mail.SecondContextId is { } secondContextId)
        {
            await FreezeSentReportSpecificationAsync(db, mail, secondContextId, actor, observedAtUtc, cancellationToken);
        }
    }

    private static async Task FreezeSentReportSpecificationAsync(
        PegasusDbContext db,
        StaffMailSendOperationEntity mail,
        Guid generationId,
        ActionActor actor,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var generation = await db.Set<CaseReportGenerationEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == generationId, cancellationToken)
            ?? throw new InvalidOperationException("The sent report generation is unavailable.");
        var snapshot = JsonSerializer.Deserialize<CaseReportGenerationSnapshot>(
            generation.SnapshotJson,
            ReportSnapshotJson)
            ?? throw new InvalidOperationException("The sent report generation has no snapshot.");
        var estimate = snapshot.CurrentEstimate;
        if (estimate is null
            || estimate.CaseId != generation.CaseId
            || estimate.SpecificationId != snapshot.CurrentEstimateId
            || estimate.Version != snapshot.CurrentEstimateVersion)
        {
            throw new InvalidOperationException("The sent report has no matching frozen repair specification.");
        }

        var origin = $"staff-mail-sent:{mail.Id:N}";
        if (await db.CaseRepairSpecificationSnapshots.AsNoTracking().AnyAsync(item =>
                item.SpecificationId == estimate.SpecificationId
                && item.Kind == nameof(RepairSpecificationSnapshotKind.Sent)
                && item.Origin == origin,
                cancellationToken))
        {
            throw new InvalidOperationException("The report's Sent repair specification was already marked.");
        }

        var latest = await db.CaseRepairSpecificationSnapshots.AsNoTracking()
            .Where(item => item.SpecificationId == estimate.SpecificationId)
            .OrderByDescending(item => item.Number)
            .FirstOrDefaultAsync(cancellationToken);
        // The Sent version is frozen in the work the report was made from.
        EfRepairSpecificationSnapshotStore.Freeze(
            db, generation.WorkId, estimate, actor, RepairSpecificationSnapshotKind.Sent,
            origin, observedAtUtc, latest);
    }

    /// <summary>
    /// Records a Case chaser that reached Sent as the Case's chase: the due
    /// work's most recent attempt and next chase move exactly as a recorded
    /// manual chase would, attributed to whoever sent it: the member of staff or
    /// the Automation Actor. Runs
    /// inside the Sent observation, so every case it does not apply to returns
    /// rather than throws; a throw here would stall the mailbox's Sent poll.
    /// </summary>
    private static async Task RecordSentChaserAsync(
        PegasusDbContext db,
        StaffMailSendOperationEntity mail,
        DateTimeOffset providerSentAtUtc,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        if (mail.Purpose != StaffMailPurpose.CaseChaser)
        {
            return;
        }

        // Lock before reading: a Serializable read taken first deadlocks
        // against a Case mutation that already holds the workflow row.
        await EfCaseWorkflowStore.AcquireWorkflowMutationLockAsync(db, mail.ContextId, cancellationToken);
        var operationKey = $"chaser-sent:{mail.Id:N}";
        if (await db.CaseManualChases.AnyAsync(
                item => item.CaseId == mail.ContextId && item.OperationKey == operationKey,
                cancellationToken))
        {
            return;
        }

        var due = await db.CaseDueWork
            .Include(item => item.Workflow)
            .ThenInclude(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == mail.ContextId, cancellationToken);
        var workflow = due?.Workflow;
        if (workflow is null
            || due is null
            || workflow.ArchivedAtUtc is not null
            || workflow.State != nameof(CaseLifecycleState.NotReady)
            || due.State != nameof(CaseDueWorkState.Scheduled))
        {
            return;
        }

        var recipients = JsonSerializer.Deserialize<Recipients>(mail.RecipientsJson);
        var target = ChaseTarget(recipients?.To?.Select(value => value.Address).ToArray() ?? []);
        // The Worker observes the send, so the chase carries the roles the
        // prepared send recorded; the Worker does not read the staff role tables.
        var roles = await PreparedRolesAsync(db, mail.Id, cancellationToken);
        var interval = (await EfWorkflowConfigurationStore.ReadAsync(db, cancellationToken)).ChaseIntervalDays;
        EfCaseWorkflowStore.ApplyChase(
            db,
            workflow,
            due,
            channel: "E-mail",
            target,
            attemptedAtUtc: providerSentAtUtc,
            outcome: "Sent",
            note: null,
            operationKey,
            requestHash: mail.PayloadHash,
            mail.ActorKind,
            mail.ActorSubjectId,
            roles,
            eventType: "chaser_sent",
            occurredAtUtc: observedAtUtc,
            chaseIntervalDays: interval);
    }

    /// <summary>
    /// The addresses a chaser went to, within the chase record's 500
    /// characters: all of them, else the first and how many more, else the
    /// first cut to fit.
    /// </summary>
    private static string ChaseTarget(string[] addresses)
    {
        const int maximumLength = 500;
        var joined = string.Join("; ", addresses);
        if (joined.Length <= maximumLength)
        {
            return joined;
        }

        var summary = $"{addresses[0]} +{addresses.Length - 1} more";
        return summary.Length <= maximumLength ? summary : summary[..maximumLength];
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
            // A report of either work is sent against its Case: the
            // Inspection's report stays sendable on its own work once the
            // Audit exists (operator, 1 October 2026), and one send may carry
            // both works' reports (operator, 12 October 2026). Which works the
            // send proves is read from the generations when the evidence is
            // linked. Every generation the send carried must be the same
            // Case's, at its frozen version where that is required.
            var carried = entity.SecondContextId is { } secondContextId
                ? new[] { (Id: entity.ContextId, Version: entity.ContextVersion), (Id: secondContextId, Version: entity.SecondContextVersion ?? 0) }
                : [(Id: entity.ContextId, Version: entity.ContextVersion)];
            var carriedIds = carried.Select(value => value.Id).ToArray();
            var generations = await db.Set<CaseReportGenerationEntity>().AsNoTracking()
                .Where(value => carriedIds.Contains(value.Id))
                .Select(value => new { value.Id, value.CaseId, value.Version })
                .ToArrayAsync(cancellationToken);
            var cases = carried
                .Select(wanted => generations.SingleOrDefault(value => value.Id == wanted.Id
                    && (!requireFrozenGenerationVersion || value.Version == wanted.Version))?.CaseId)
                .Distinct()
                .ToArray();
            caseId = cases is [{ } onlyCase] ? onlyCase : null;
            if (caseId is not null)
            {
                var generated = await db.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                    .Where(value => carriedIds.Contains(value.GenerationId)
                        && value.VersionId != null && value.Sha256 != null)
                    .Select(value => new { VersionId = value.VersionId!.Value, Sha256 = value.Sha256! })
                    .ToArrayAsync(cancellationToken);
                var frozen = attachments
                    .Select(value => (value.VersionId, value.Sha256))
                    .OrderBy(value => value.VersionId)
                    .ThenBy(value => value.Sha256, StringComparer.Ordinal)
                    .ToArray();
                var remaining = generated
                    .Select(value => (VersionId: (Guid?)value.VersionId, value.Sha256))
                    .ToList();
                // The Principal's rules may attach the Case's recognised filed
                // estimates beside the generated documents (#1015).
                remaining.AddRange(
                    (await EfFiledEstimateAttachmentQueries.ReadAsync(db, caseId.Value, cancellationToken).ConfigureAwait(false))
                        .Select(estimate => (estimate.Attachment.VersionId, estimate.Attachment.Sha256)));
                foreach (var wanted in frozen)
                {
                    var index = remaining.FindIndex(value =>
                        value.VersionId == wanted.VersionId
                        && string.Equals(value.Sha256, wanted.Sha256, StringComparison.Ordinal));
                    if (index < 0)
                    {
                        caseId = null;
                        break;
                    }

                    remaining.RemoveAt(index);
                }
            }
        }
        return new(
            entity.ActorSubjectId, Map(entity), entity.DraftImmutableId, attachments,
            entity.Purpose, entity.ContextId, entity.ContextVersion, caseId,
            entity.SecondContextId);
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
        value.Purpose, value.ContextId, value.ContextVersion, value.OriginalRetainedMessageId,
        value.ReconciliationContinuation, value.DraftImmutableId);

    private sealed record Recipients(
        IReadOnlyList<StaffMailRecipient> To,
        IReadOnlyList<StaffMailRecipient> Cc);
}
