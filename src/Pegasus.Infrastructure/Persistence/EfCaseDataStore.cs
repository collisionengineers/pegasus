using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfCaseDataStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    IEnumerable<IProviderCaseMatchPolicy>? caseMatchPolicies = null) : ICaseDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CaseDataProjection?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var snapshot = await SnapshotQuery(context, tracking: false)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        var workflow = await context.CaseWorkflows.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
            ?? throw new InvalidDataException(
                "The accepted case data snapshot has no workflow record.");
        return Map(snapshot, workflow);
    }

    public async Task<CaseDataProjection> ConfirmCompletenessAsync(
        ConfirmCompletenessRequest request,
        CaseCompletenessEvaluation evaluation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(evaluation);
        CaseDataPolicy.ValidateMutation(request);
        CaseDataPolicy.ValidateCompleteness(request.Completeness);
        ValidateEvaluation(evaluation);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = RequestHash(
            "confirm_completeness",
            request,
            request.Completeness,
            evaluation);
        var replay = await FindReplayAsync(
            context,
            request.CaseId,
            request.OperationKey,
            requestHash,
            cancellationToken);
        if (replay)
        {
            return await GetRequiredProjectionAsync(
                context,
                request.CaseId,
                tracking: false,
                cancellationToken);
        }

        var (snapshot, workflow) = await GetRequiredForMutationAsync(
            context,
            request.CaseId,
            cancellationToken);
        RequireVersion(workflow, request.ExpectedVersion);
        RequireLease(workflow, request.Actor, request.EditLeaseToken, UtcNow());
        ArchivedCaseGuard.RequireMutable(workflow);
        if (workflow.AssignedEngineerId is not null
            || workflow.State is not (
                nameof(CaseLifecycleState.NotReady)
                or nameof(CaseLifecycleState.Review)))
        {
            throw new InvalidOperationException(
                "Completeness can be changed only before Engineer assignment on a Not ready or Review case.");
        }

        var before = new CaseCompleteness(
            snapshot.Case.InstructionComplete,
            snapshot.Case.ImagesComplete,
            snapshot.Case.InstructionConfirmedByStaff,
            snapshot.Case.ImagesConfirmedByStaff);
        var beforeJson = JsonSerializer.Serialize(before, JsonOptions);
        snapshot.Case.InstructionComplete = request.Completeness.InstructionComplete;
        snapshot.Case.ImagesComplete = request.Completeness.ImagesComplete;
        snapshot.Case.InstructionConfirmedByStaff = request.Completeness.InstructionConfirmedByStaff;
        snapshot.Case.ImagesConfirmedByStaff = request.Completeness.ImagesConfirmedByStaff;
        snapshot.CompletenessPolicyKey = evaluation.PolicyKey;
        snapshot.CompletenessPolicyVersion = evaluation.PolicyVersion;
        snapshot.CompletenessPolicySatisfied = evaluation.SatisfiesPolicy;

        var now = UtcNow();
        if (evaluation.SatisfiesPolicy)
        {
            workflow.State = nameof(CaseLifecycleState.Review);
            CaseChaseState.Stop(workflow);
        }
        else
        {
            workflow.State = nameof(CaseLifecycleState.NotReady);
            ScheduleDueWork(context, workflow, snapshot.Case.AcceptedInspectionDeadline, now);
        }

        var beforeVersion = workflow.Version;
        workflow.Version++;
        ClearLease(workflow);
        AddHistory(
            context,
            workflow,
            request,
            requestHash,
            "case_completeness_confirmed",
            beforeVersion,
            workflow.Version,
            beforeJson,
            JsonSerializer.Serialize(request.Completeness, JsonOptions),
            $"{evaluation.PolicyKey}/v{evaluation.PolicyVersion}",
            now);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CaseVersionConflictException(
                request.CaseId,
                request.ExpectedVersion,
                request.ExpectedVersion + 1);
        }

        return Map(snapshot, workflow);
    }

    public async Task<CaseDataProjection> SaveAsync(
        SaveCaseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        CaseDataPolicy.ValidateMutation(request);
        var data = CaseDataPolicy.Normalize(request.Data);
        request = request with { Data = data };

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = RequestHash("save_case", request, data, policy: null);
        var replay = await FindReplayAsync(
            context,
            request.CaseId,
            request.OperationKey,
            requestHash,
            cancellationToken);
        if (replay)
        {
            return await GetRequiredProjectionAsync(
                context,
                request.CaseId,
                tracking: false,
                cancellationToken);
        }

        var (snapshot, workflow) = await GetRequiredForMutationAsync(
            context,
            request.CaseId,
            cancellationToken);
        RequireVersion(workflow, request.ExpectedVersion);
        RequireLease(workflow, request.Actor, request.EditLeaseToken, UtcNow());
        ArchivedCaseGuard.RequireMutable(workflow);
        if (!Enum.TryParse<CaseLifecycleState>(workflow.State, out var state)
            || workflow.AssignedEngineerId is not null
            || state is not (CaseLifecycleState.NotReady or CaseLifecycleState.Review))
        {
            throw new InvalidOperationException(
                "Case data can be saved only before Engineer assignment on a Not ready or Review case.");
        }

        var before = EditableData(snapshot);
        var completenessBefore = new CaseCompleteness(
            snapshot.Case.InstructionComplete,
            snapshot.Case.ImagesComplete,
            snapshot.Case.InstructionConfirmedByStaff,
            snapshot.Case.ImagesConfirmedByStaff);
        if (before == data)
        {
            throw new InvalidOperationException("SaveCase requires at least one changed confirmed value.");
        }

        var now = UtcNow();
        ApplyEditableData(context, snapshot, data, request.Actor, now);
        CaseMatchIndexProjector.Apply(
            context,
            await context.CaseMatchIndex.SingleOrDefaultAsync(
                item => item.CaseId == request.CaseId,
                cancellationToken),
            CaseMatchIndexProjector.Project(
                snapshot.Case,
                snapshot.Fields,
                caseMatchPolicies ?? [],
                now));
        snapshot.Case.AcceptedInspectionDeadline = data.InspectionDeadline;
        snapshot.Case.InstructionComplete = false;
        snapshot.Case.InstructionConfirmedByStaff = false;
        snapshot.CompletenessPolicySatisfied = false;
        workflow.State = nameof(CaseLifecycleState.NotReady);
        ScheduleDueWork(context, workflow, data.InspectionDeadline, now);

        var beforeVersion = workflow.Version;
        workflow.Version++;
        var completenessAfter = new CaseCompleteness(
            snapshot.Case.InstructionComplete,
            snapshot.Case.ImagesComplete,
            snapshot.Case.InstructionConfirmedByStaff,
            snapshot.Case.ImagesConfirmedByStaff);
        ClearLease(workflow);
        AddHistory(
            context,
            workflow,
            request,
            requestHash,
            "case_data_saved",
            beforeVersion,
            workflow.Version,
            JsonSerializer.Serialize(
                new { Data = before, Completeness = completenessBefore },
                JsonOptions),
            JsonSerializer.Serialize(
                new { Data = data, Completeness = completenessAfter },
                JsonOptions),
            $"{CaseDataPolicy.EditPolicyKey}/v{CaseDataPolicy.EditPolicyVersion}",
            now);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CaseVersionConflictException(
                request.CaseId,
                request.ExpectedVersion,
                request.ExpectedVersion + 1);
        }

        return Map(snapshot, workflow);
    }

    private static async Task<bool> FindReplayAsync(
        PegasusDbContext context,
        Guid caseId,
        string operationKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var replay = await context.CaseWorkflowEvents.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == caseId && item.OperationKey == operationKey,
                cancellationToken);
        if (replay is null)
        {
            return false;
        }

        if (!FixedTimeEquals(replay.RequestHash, requestHash))
        {
            throw new CaseOperationConflictException(caseId, operationKey);
        }

        return true;
    }

    private static async Task<(CaseDataSnapshotEntity Snapshot, CaseWorkflowEntity Workflow)>
        GetRequiredForMutationAsync(
            PegasusDbContext context,
            Guid caseId,
            CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotQuery(context, tracking: true)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");
        var workflow = await context.CaseWorkflows
            .Include(item => item.DueWork)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
            ?? throw new InvalidDataException(
                "The accepted case data snapshot has no workflow record.");
        return (snapshot, workflow);
    }

    private static async Task<CaseDataProjection> GetRequiredProjectionAsync(
        PegasusDbContext context,
        Guid caseId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotQuery(context, tracking)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");
        var workflowQuery = tracking
            ? context.CaseWorkflows
            : context.CaseWorkflows.AsNoTracking();
        var workflow = await workflowQuery
            .SingleAsync(item => item.CaseId == caseId, cancellationToken);
        return Map(snapshot, workflow);
    }

    private static IQueryable<CaseDataSnapshotEntity> SnapshotQuery(
        PegasusDbContext context,
        bool tracking)
    {
        var query = context.CaseDataSnapshots
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .Include(item => item.Fields);
        return tracking ? query : query.AsNoTracking();
    }

    private static void ApplyEditableData(
        PegasusDbContext context,
        CaseDataSnapshotEntity snapshot,
        CaseEditableData data,
        ActionActor actor,
        DateTimeOffset now)
    {
        SetConfirmed(context, snapshot, CaseDataFieldNames.ClaimantName, CaseDataCodes.Text, data.ClaimantName, actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.ClaimNumber, CaseDataCodes.Text, data.ClaimNumber, actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.VehicleRegistration, CaseDataCodes.Text, data.VehicleRegistration, actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.VehicleMake, CaseDataCodes.Text, data.VehicleMake, actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.VehicleModel, CaseDataCodes.Text, data.VehicleModel, actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.VehicleMileage, CaseDataCodes.Integer, Integer(data.VehicleMileage), actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.VehicleMileageUnit, CaseDataCodes.Text, data.VehicleMileageUnit, actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.AccidentCircumstances, CaseDataCodes.Text, data.AccidentCircumstances, actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.IncidentDate, CaseDataCodes.Date, Date(data.IncidentDate), actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.ContactName, CaseDataCodes.Text, data.ContactName, actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.ContactEmailAddress, CaseDataCodes.Text, data.ContactEmailAddress, actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.ContactPhoneNumber, CaseDataCodes.Text, data.ContactPhoneNumber, actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.InstructionDate, CaseDataCodes.Date, Date(data.InstructionDate), actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.VatStatus, CaseDataCodes.Text, data.VatStatus, actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.InspectionDate, CaseDataCodes.Date, Date(data.InspectionDate), actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.InspectionDeadline, CaseDataCodes.Date, Date(data.InspectionDeadline), actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.InspectionAddress, CaseDataCodes.Text, data.InspectionAddress, actor, now);
        SetConfirmed(context, snapshot, CaseDataFieldNames.InspectionMode, CaseDataCodes.InspectionMode, InspectionMode(data.InspectionMode), actor, now);
    }

    private static void SetConfirmed(
        PegasusDbContext context,
        CaseDataSnapshotEntity snapshot,
        string fieldName,
        string valueType,
        string? value,
        ActionActor actor,
        DateTimeOffset now)
    {
        var existing = snapshot.Fields.SingleOrDefault(
            item => item.FieldName == fieldName && item.ValueKind == CaseDataCodes.Confirmed);
        if (value is null)
        {
            if (existing is not null)
            {
                context.CaseDataFields.Remove(existing);
                snapshot.Fields.Remove(existing);
            }
            return;
        }

        var underlying = snapshot.Fields.SingleOrDefault(
            item => item.FieldName == fieldName
                && item.ValueKind is CaseDataCodes.Fact or CaseDataCodes.Suggestion
                && string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new()
            {
                CaseId = snapshot.CaseId,
                Snapshot = snapshot,
                FieldName = fieldName,
                ValueKind = CaseDataCodes.Confirmed,
                ValueType = valueType,
                Value = value,
                SourceKind = CaseDataCodes.StaffCorrection,
                SourceIdentity = actor.SubjectId,
                SourceLabel = "staff case-data confirmation",
                PolicyKey = CaseDataPolicy.EditPolicyKey,
                PolicyVersion = CaseDataPolicy.EditPolicyVersion,
                ConfirmedByActor = actor.SubjectId,
                ConfirmedAtUtc = now
            };
            snapshot.Fields.Add(existing);
        }
        else
        {
            existing.ValueType = valueType;
            existing.Value = value;
            existing.ConfirmedByActor = actor.SubjectId;
            existing.ConfirmedAtUtc = now;
        }

        existing.SourceKind = underlying?.SourceKind ?? CaseDataCodes.StaffCorrection;
        existing.SourceIdentity = underlying?.SourceIdentity ?? actor.SubjectId;
        existing.SourceLabel = underlying?.SourceLabel ?? "staff case-data correction";
        existing.PolicyKey = underlying?.PolicyKey ?? CaseDataPolicy.EditPolicyKey;
        existing.PolicyVersion = underlying?.PolicyVersion ?? CaseDataPolicy.EditPolicyVersion;
    }

    private static CaseEditableData EditableData(CaseDataSnapshotEntity snapshot) => new(
        ConfirmedText(snapshot, CaseDataFieldNames.ClaimantName),
        ConfirmedText(snapshot, CaseDataFieldNames.ClaimNumber),
        ConfirmedText(snapshot, CaseDataFieldNames.VehicleRegistration),
        ConfirmedText(snapshot, CaseDataFieldNames.VehicleMake),
        ConfirmedText(snapshot, CaseDataFieldNames.VehicleModel),
        ConfirmedLong(snapshot, CaseDataFieldNames.VehicleMileage),
        ConfirmedText(snapshot, CaseDataFieldNames.VehicleMileageUnit),
        ConfirmedText(snapshot, CaseDataFieldNames.AccidentCircumstances),
        ConfirmedDate(snapshot, CaseDataFieldNames.IncidentDate),
        ConfirmedText(snapshot, CaseDataFieldNames.ContactName),
        ConfirmedText(snapshot, CaseDataFieldNames.ContactEmailAddress),
        ConfirmedText(snapshot, CaseDataFieldNames.ContactPhoneNumber),
        ConfirmedDate(snapshot, CaseDataFieldNames.InstructionDate),
        ConfirmedText(snapshot, CaseDataFieldNames.VatStatus),
        ConfirmedDate(snapshot, CaseDataFieldNames.InspectionDate),
        ConfirmedDate(snapshot, CaseDataFieldNames.InspectionDeadline),
        ConfirmedText(snapshot, CaseDataFieldNames.InspectionAddress),
        ConfirmedInspectionMode(snapshot, CaseDataFieldNames.InspectionMode));

    private static string? ConfirmedText(CaseDataSnapshotEntity snapshot, string name) =>
        Confirmed(snapshot, name)?.Value;

    private static long? ConfirmedLong(CaseDataSnapshotEntity snapshot, string name) =>
        Confirmed(snapshot, name) is { } field
            ? long.Parse(field.Value, NumberStyles.None, CultureInfo.InvariantCulture)
            : null;

    private static DateOnly? ConfirmedDate(CaseDataSnapshotEntity snapshot, string name) =>
        Confirmed(snapshot, name) is { } field
            ? DateOnly.ParseExact(field.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

    private static CaseInspectionMode? ConfirmedInspectionMode(
        CaseDataSnapshotEntity snapshot,
        string name) =>
        Confirmed(snapshot, name) is { } field
            ? ParseInspectionMode(field.Value)
            : null;

    private static CaseDataFieldEntity? Confirmed(CaseDataSnapshotEntity snapshot, string name) =>
        snapshot.Fields.SingleOrDefault(
            item => item.FieldName == name && item.ValueKind == CaseDataCodes.Confirmed);

    private static void RequireVersion(CaseWorkflowEntity workflow, long expectedVersion) =>
        CaseMutationGuard.RequireVersion(workflow, expectedVersion);

    private static void RequireLease(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string token,
        DateTimeOffset now) =>
        CaseMutationGuard.RequireLease(workflow, actor, token, now);

    private static void ClearLease(CaseWorkflowEntity workflow) =>
        CaseMutationGuard.ClearLease(workflow);


    private static void ScheduleDueWork(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        DateOnly? dueBy,
        DateTimeOffset now)
    {
        if (workflow.DueWork is not { } due)
        {
            due = new()
            {
                CaseId = workflow.CaseId,
                Workflow = workflow,
                MissingMaterialReason = "Case completeness is not confirmed",
                DueBy = dueBy,
                State = nameof(CaseDueWorkState.Scheduled),
                NextChaseAtUtc = CaseChaseSchedule.FirstChaseAt(now),
                Version = 0
            };
            workflow.DueWork = due;
            context.CaseDueWork.Add(due);
            return;
        }

        due.MissingMaterialReason = "Case completeness is not confirmed";
        due.DueBy = dueBy;
        due.State = nameof(CaseDueWorkState.Scheduled);
        due.NextChaseAtUtc = CaseChaseSchedule.FirstChaseAt(now);
        due.HeldAtUtc = null;
        due.RemainingChaseIntervalTicks = null;
        due.Version++;
    }

    private static void AddHistory(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        CaseMutationRequest request,
        string requestHash,
        string eventType,
        long beforeVersion,
        long afterVersion,
        string beforeJson,
        string afterJson,
        string policyVersion,
        DateTimeOffset occurredAtUtc)
    {
        var rolesJson = JsonSerializer.Serialize(
            request.Actor.Roles.OrderBy(role => role),
            JsonOptions);
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = eventType,
            OperationKey = request.OperationKey,
            RequestHash = requestHash,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = rolesJson,
            Reason = request.Reason,
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = afterVersion
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = workflow.CaseId.ToString("D"),
            EventKind = eventType,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = rolesJson,
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = request.OperationKey,
            Reason = request.Reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            PolicyVersion = policyVersion
        });
        context.CaseHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Case = workflow.Case,
            EventType = eventType,
            Actor = request.Actor.SubjectId,
            Reason = request.Reason,
            OccurredAtUtc = occurredAtUtc,
            OperationKey = request.OperationKey,
            BeforeVersion = beforeVersion,
            AfterVersion = afterVersion
        });
    }

    private static CaseDataProjection Map(
        CaseDataSnapshotEntity snapshot,
        CaseWorkflowEntity workflow) => new(
        new(
            snapshot.CaseId,
            snapshot.Case.Principal.Code,
            snapshot.Case.Year,
            snapshot.Case.Sequence,
            snapshot.Case.Reference,
            snapshot.Case.AuditReference),
        new(
            snapshot.OriginIntakeReceiptId,
            ParseSourceChannel(snapshot.OriginSourceChannel),
            snapshot.OriginExternalReceiptToken,
            snapshot.OriginSourceHash,
            snapshot.OriginReceivedAtUtc,
            snapshot.SourceReaderKey,
            snapshot.SourceReaderVersion,
            snapshot.ExtractionPolicyKey,
            snapshot.ExtractionPolicyVersion),
        snapshot.AcceptedAtUtc,
        workflow.Version,
        ParseLifecycleState(workflow.State),
        new(
            new(
                snapshot.Case.InstructionComplete,
                snapshot.Case.ImagesComplete,
                snapshot.Case.InstructionConfirmedByStaff,
                snapshot.Case.ImagesConfirmedByStaff),
            new(
                snapshot.CompletenessPolicySatisfied,
                snapshot.CompletenessPolicyKey,
                snapshot.CompletenessPolicyVersion)),
        new(TextField(snapshot, CaseDataFieldNames.WorkProviderCode)),
        new(TextField(snapshot, CaseDataFieldNames.ClaimantName)),
        new(TextField(snapshot, CaseDataFieldNames.ClaimNumber)),
        new(
            TextField(snapshot, CaseDataFieldNames.VehicleRegistration),
            TextField(snapshot, CaseDataFieldNames.VehicleMake),
            TextField(snapshot, CaseDataFieldNames.VehicleModel),
            LongField(snapshot, CaseDataFieldNames.VehicleMileage),
            TextField(snapshot, CaseDataFieldNames.VehicleMileageUnit)),
        new(
            DateField(snapshot, CaseDataFieldNames.IncidentDate),
            TextField(snapshot, CaseDataFieldNames.AccidentCircumstances)),
        new(
            TextField(snapshot, CaseDataFieldNames.ContactName),
            TextField(snapshot, CaseDataFieldNames.ContactEmailAddress),
            TextField(snapshot, CaseDataFieldNames.ContactPhoneNumber)),
        new(
            DateField(snapshot, CaseDataFieldNames.InstructionDate),
            TextField(snapshot, CaseDataFieldNames.VatStatus)),
        new(
            DateField(snapshot, CaseDataFieldNames.InspectionDate),
            DateField(snapshot, CaseDataFieldNames.InspectionDeadline),
            TextField(snapshot, CaseDataFieldNames.InspectionAddress),
            InspectionModeField(snapshot, CaseDataFieldNames.InspectionMode)));

    private static CaseField<string> TextField(
        CaseDataSnapshotEntity snapshot,
        string fieldName) => Field(snapshot, fieldName, value => value);

    private static CaseField<long> LongField(
        CaseDataSnapshotEntity snapshot,
        string fieldName) => Field(
        snapshot,
        fieldName,
        value => long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture));

    private static CaseField<DateOnly> DateField(
        CaseDataSnapshotEntity snapshot,
        string fieldName) => Field(
        snapshot,
        fieldName,
        value => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static CaseField<CaseInspectionMode> InspectionModeField(
        CaseDataSnapshotEntity snapshot,
        string fieldName) => Field(snapshot, fieldName, ParseInspectionMode);

    private static CaseField<T> Field<T>(
        CaseDataSnapshotEntity snapshot,
        string fieldName,
        Func<string, T> parse)
        where T : notnull
    {
        var values = snapshot.Fields.Where(item => item.FieldName == fieldName).ToArray();
        return new(
            MapValue(values, CaseDataCodes.Fact, parse),
            MapValue(values, CaseDataCodes.Suggestion, parse),
            MapValue(values, CaseDataCodes.Confirmed, parse));
    }

    private static CaseDataValue<T>? MapValue<T>(
        IReadOnlyList<CaseDataFieldEntity> values,
        string kind,
        Func<string, T> parse)
        where T : notnull
    {
        var value = values.SingleOrDefault(item => item.ValueKind == kind);
        if (value is null)
        {
            return null;
        }

        return new(
            parse(value.Value),
            ParseValueKind(value.ValueKind),
            new(
                ParseSourceKind(value.SourceKind),
                value.SourceIdentity,
                value.SourceLabel,
                value.PolicyKey,
                value.PolicyVersion),
            value.ConfirmedByActor,
            value.ConfirmedAtUtc);
    }

    private DateTimeOffset UtcNow()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static string RequestHash(
        string command,
        CaseMutationRequest request,
        object payload,
        CaseCompletenessEvaluation? policy)
    {
        var material = JsonSerializer.Serialize(new
        {
            Command = command,
            request.CaseId,
            request.ExpectedVersion,
            ActorKind = request.Actor.Kind.ToString(),
            request.Actor.SubjectId,
            Roles = request.Actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray(),
            request.OperationKey,
            request.Reason,
            request.EditLeaseToken,
            Payload = payload,
            Policy = policy
        }, JsonOptions);
        return Hash(material);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool FixedTimeEquals(string left, string right)
    {
        if (left.Length != 64 || right.Length != 64
            || left.Any(character => !char.IsAsciiHexDigit(character))
            || right.Any(character => !char.IsAsciiHexDigit(character)))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));
    }

    private static void ValidateEvaluation(CaseCompletenessEvaluation evaluation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evaluation.PolicyKey);
        if (evaluation.PolicyKey.Length > 100 || evaluation.PolicyVersion < 1)
        {
            throw new InvalidOperationException(
                "The completeness-policy identity is invalid.");
        }
    }

    private static string? Integer(long? value) =>
        value?.ToString(CultureInfo.InvariantCulture);

    private static string? Date(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? InspectionMode(CaseInspectionMode? value) => value switch
    {
        null => null,
        CaseInspectionMode.PhysicalAddress => "physical_address",
        CaseInspectionMode.ImageBasedAssessment => "image_based_assessment",
        _ => throw new InvalidDataException("The inspection mode is invalid.")
    };

    private static CaseInspectionMode ParseInspectionMode(string value) => value switch
    {
        "physical_address" => CaseInspectionMode.PhysicalAddress,
        "image_based_assessment" => CaseInspectionMode.ImageBasedAssessment,
        _ => throw new InvalidDataException(
            $"Unknown persisted inspection mode '{value}'.")
    };

    private static IntakeSourceChannel ParseSourceChannel(string value) => value switch
    {
        "manual_upload" => IntakeSourceChannel.ManualUpload,
        "mailbox" => IntakeSourceChannel.Mailbox,
        "automation" => IntakeSourceChannel.Automation,
        _ => throw new InvalidDataException(
            $"Unknown persisted intake source channel '{value}'.")
    };

    private static CaseLifecycleState ParseLifecycleState(string value) =>
        Enum.TryParse<CaseLifecycleState>(value, out var state)
            ? state
            : throw new InvalidDataException(
                $"Unknown persisted case lifecycle state '{value}'.");

    private static CaseDataValueKind ParseValueKind(string value) => value switch
    {
        CaseDataCodes.Fact => CaseDataValueKind.Fact,
        CaseDataCodes.Suggestion => CaseDataValueKind.Suggestion,
        CaseDataCodes.Confirmed => CaseDataValueKind.Confirmed,
        _ => throw new InvalidDataException(
            $"Unknown persisted case-data value kind '{value}'.")
    };

    private static CaseDataSourceKind ParseSourceKind(string value) => value switch
    {
        CaseDataCodes.IntakeEvidence => CaseDataSourceKind.IntakeEvidence,
        CaseDataCodes.MailRoute => CaseDataSourceKind.MailRoute,
        CaseDataCodes.CaseAcceptance => CaseDataSourceKind.CaseAcceptance,
        CaseDataCodes.StaffCorrection => CaseDataSourceKind.StaffCorrection,
        CaseDataCodes.VehicleLookup => CaseDataSourceKind.VehicleLookup,
        CaseDataCodes.ProviderSetting => CaseDataSourceKind.ProviderSetting,
        _ => throw new InvalidDataException(
            $"Unknown persisted case-data source kind '{value}'.")
    };
}
