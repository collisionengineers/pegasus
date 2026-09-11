using System.Diagnostics;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfVehicleWorkflowStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider)
    : IRequestVehicleLookupStore, IVehicleEvidenceQueries, IAutomaticVehicleLookupStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] VehicleFieldNames =
    [
        CaseDataFieldNames.VehicleRegistration,
        CaseDataFieldNames.VehicleMake,
        CaseDataFieldNames.VehicleModel,
        CaseDataFieldNames.VehicleMileage,
        CaseDataFieldNames.VehicleMileageUnit
    ];

    public async Task<RequestedVehicleLookup> RequestAsync(
        RequestVehicleLookupCommand command,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await RequestOnceAsync(command, cancellationToken);
            }
            catch (Exception exception)
                when (attempt < 3 && IsRetryableConcurrencyFailure(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new UnreachableException();
    }

    private async Task<RequestedVehicleLookup> RequestOnceAsync(
        RequestVehicleLookupCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var fingerprint = RequestFingerprint(command);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.Set<VehicleLookupRequestEntity>()
            .AsNoTracking()
            .Include(item => item.WorkItem)
            .SingleOrDefaultAsync(
                item => item.CaseId == command.CaseId
                    && item.OperationKey == command.OperationKey,
                cancellationToken);
        if (replay is not null)
        {
            RequireMatchingFingerprint(
                command.CaseId,
                command.OperationKey,
                replay.RequestFingerprint,
                fingerprint);
            return new(
                replay.WorkItemId,
                replay.CaseId,
                replay.Registration,
                EfVehicleLookupWorkStore.MapWorkState(replay.WorkItem),
                replay.ResultingCaseVersion,
                IsReplay: true);
        }

        if (await OperationKeyExistsAsync(context, command.CaseId, command.OperationKey, cancellationToken))
        {
            throw new VehicleOperationConflictException(command.CaseId, command.OperationKey);
        }

        await ArchivedCaseGuard.RequireMutableAsync(context, command.CaseId, cancellationToken);
        var workflow = await context.CaseWorkflows
            .SingleAsync(item => item.CaseId == command.CaseId, cancellationToken);
        RequireVersion(workflow, command.ExpectedCaseVersion);
        RequireVehicleDataWritable(workflow);
        RequireLease(workflow, command.Actor, command.EditLeaseToken, UtcNow());

        var confirmedRegistrations = await context.CaseDataFields
            .AsNoTracking()
            .Where(item => item.CaseId == command.CaseId
                && item.FieldName == CaseDataFieldNames.VehicleRegistration
                && item.ValueKind == CaseDataCodes.Confirmed)
            .OrderBy(item => item.SourceIdentity)
            .Select(item => item.Value)
            .Take(2)
            .ToArrayAsync(cancellationToken);
        if (confirmedRegistrations.Length > 1)
        {
            throw new AcceptedVehicleRegistrationRequiredException(
                command.CaseId,
                confirmedRegistrations.Length);
        }

        var acceptedRegistrations = confirmedRegistrations.Length == 1
            ? confirmedRegistrations
            : await context.CaseDataFields
                .AsNoTracking()
                .Where(item => item.CaseId == command.CaseId
                    && item.FieldName == CaseDataFieldNames.VehicleRegistration
                    && item.ValueKind == CaseDataCodes.Fact)
                .OrderBy(item => item.SourceIdentity)
                .Select(item => item.Value)
                .Take(2)
                .ToArrayAsync(cancellationToken);
        if (acceptedRegistrations.Length != 1)
        {
            throw new AcceptedVehicleRegistrationRequiredException(
                command.CaseId,
                acceptedRegistrations.Length);
        }

        var acceptedRegistration = acceptedRegistrations[0];
        if (!string.Equals(acceptedRegistration, command.Registration, StringComparison.Ordinal))
        {
            throw new AcceptedVehicleRegistrationConflictException(
                command.CaseId,
                acceptedRegistration,
                command.Registration);
        }

        var nowUtc = UtcNow();
        var beforeVersion = workflow.Version;
        workflow.Version = checked(workflow.Version + 1);
        ClearLease(workflow);
        var workItemId = Guid.NewGuid();
        context.ExternalWorkItems.Add(new()
        {
            Id = workItemId,
            CaseId = command.CaseId,
            Kind = Pegasus.Core.Custody.ExternalWorkKinds.VehicleLookup,
            OperationKey = command.OperationKey,
            State = "pending",
            AttemptCount = 0,
            DueAtUtc = nowUtc
        });
        context.Set<VehicleLookupRequestEntity>().Add(new()
        {
            WorkItemId = workItemId,
            CaseId = command.CaseId,
            Registration = command.Registration,
            OperationKey = command.OperationKey,
            RequestFingerprint = fingerprint,
            RequestedByKind = command.Actor.Kind.ToString(),
            RequestedBySubjectId = command.Actor.SubjectId,
            RequestedByRolesJson = RolesJson(command.Actor),
            RequestedAtUtc = nowUtc,
            ResultingCaseVersion = workflow.Version
        });
        AddWorkflowEvent(
            context,
            workflow,
            command.Actor,
            command.OperationKey,
            "Vehicle lookup requested by staff.",
            fingerprint,
            "vehicle_lookup_requested",
            beforeVersion,
            workflow.Version,
            nowUtc);
        AddActionHistory(
            context,
            command.CaseId,
            command.Actor,
            command.OperationKey,
            "vehicle_lookup_requested",
            "Vehicle lookup requested by staff.",
            beforeVersion,
            workflow.Version,
            new { workItemId, command.Registration },
            nowUtc);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            workItemId,
            command.CaseId,
            command.Registration,
            VehicleLookupWorkState.Pending,
            workflow.Version,
            IsReplay: false);
    }

    public async Task<CaseVehicleEvidence?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await context.Cases.AsNoTracking().AnyAsync(item => item.Id == caseId, cancellationToken))
        {
            return null;
        }

        var observationEntities = await context.Set<VehicleLookupObservationEntity>()
            .AsNoTracking()
            .Include(item => item.Request)
            .Where(item => item.Request.CaseId == caseId)
            .OrderBy(item => item.RecordedAtUtc)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var observations = observationEntities
            .Select(EfVehicleLookupWorkStore.MapObservation)
            .ToArray();
        var observationsById = observations.ToDictionary(item => item.Id);

        var confirmationEntities = await context.Set<VehicleConfirmationEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderBy(item => item.AfterCaseVersion)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var confirmationHistory = confirmationEntities
            .Select(MapHistory)
            .ToArray();

        var confirmedFields = await context.CaseDataFields
            .AsNoTracking()
            .Where(item => item.CaseId == caseId
                && item.ValueKind == CaseDataCodes.Confirmed
                && VehicleFieldNames.Contains(item.FieldName))
            .ToDictionaryAsync(item => item.FieldName, StringComparer.Ordinal, cancellationToken);
        var confirmed = MapConfirmed(confirmedFields, observationsById);
        return new(
            caseId,
            confirmed,
            observations.LastOrDefault(),
            observations,
            confirmationHistory);
    }

    private DateTimeOffset UtcNow()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static async Task<bool> OperationKeyExistsAsync(
        PegasusDbContext context,
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken) =>
        await context.CaseWorkflowEvents.AsNoTracking().AnyAsync(
            item => item.CaseId == caseId && item.OperationKey == operationKey,
            cancellationToken)
        || await context.ExternalWorkItems.AsNoTracking().AnyAsync(
            item => item.OperationKey == operationKey,
            cancellationToken);

    private static ConfirmedVehicleEvidence? MapConfirmed(
        Dictionary<string, CaseDataFieldEntity> fields,
        IReadOnlyDictionary<Guid, VehicleLookupObservation> observations)
    {
        if (fields.Count == 0)
        {
            return null;
        }

        return new(
            fields.TryGetValue(CaseDataFieldNames.VehicleRegistration, out var registration)
                ? MapTextField(registration, observations)
                : null,
            fields.TryGetValue(CaseDataFieldNames.VehicleMake, out var make)
                ? MapTextField(make, observations)
                : null,
            fields.TryGetValue(CaseDataFieldNames.VehicleModel, out var model)
                ? MapTextField(model, observations)
                : null,
            fields.TryGetValue(CaseDataFieldNames.VehicleMileage, out var mileage)
                ? MapLongField(mileage, observations)
                : null,
            fields.TryGetValue(CaseDataFieldNames.VehicleMileageUnit, out var unit)
                ? MapMileageUnitField(unit, observations)
                : null);
    }

    private static ConfirmedVehicleField<string> MapTextField(
        CaseDataFieldEntity field,
        IReadOnlyDictionary<Guid, VehicleLookupObservation> observations) =>
        new(
            field.Value,
            field.SourceKind,
            field.SourceIdentity,
            field.SourceLabel,
            field.PolicyKey,
            field.PolicyVersion,
            field.ConfirmedByActor
                ?? throw new InvalidDataException("Confirmed vehicle actor is missing."),
            field.ConfirmedAtUtc
                ?? throw new InvalidDataException("Confirmed vehicle time is missing."),
            FindExternalProvenance(field, observations));

    private static ConfirmedVehicleField<long> MapLongField(
        CaseDataFieldEntity field,
        IReadOnlyDictionary<Guid, VehicleLookupObservation> observations)
    {
        if (!long.TryParse(field.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            || value < 0)
        {
            throw new InvalidDataException("Confirmed vehicle mileage is invalid.");
        }
        return new(
            value,
            field.SourceKind,
            field.SourceIdentity,
            field.SourceLabel,
            field.PolicyKey,
            field.PolicyVersion,
            field.ConfirmedByActor
                ?? throw new InvalidDataException("Confirmed vehicle actor is missing."),
            field.ConfirmedAtUtc
                ?? throw new InvalidDataException("Confirmed vehicle time is missing."),
            FindExternalProvenance(field, observations));
    }

    private static ConfirmedVehicleField<VehicleMileageUnit> MapMileageUnitField(
        CaseDataFieldEntity field,
        IReadOnlyDictionary<Guid, VehicleLookupObservation> observations)
    {
        if (!Enum.TryParse<VehicleMileageUnit>(field.Value, ignoreCase: true, out var value)
            || !Enum.IsDefined(value))
        {
            throw new InvalidDataException("Confirmed vehicle mileage unit is invalid.");
        }
        return new(
            value,
            field.SourceKind,
            field.SourceIdentity,
            field.SourceLabel,
            field.PolicyKey,
            field.PolicyVersion,
            field.ConfirmedByActor
                ?? throw new InvalidDataException("Confirmed vehicle actor is missing."),
            field.ConfirmedAtUtc
                ?? throw new InvalidDataException("Confirmed vehicle time is missing."),
            FindExternalProvenance(field, observations));
    }

    private static VehicleEvidenceProvenance? FindExternalProvenance(
        CaseDataFieldEntity field,
        IReadOnlyDictionary<Guid, VehicleLookupObservation> observations) =>
        string.Equals(field.SourceKind, CaseDataCodes.VehicleLookup, StringComparison.Ordinal)
        && Guid.TryParse(field.SourceIdentity, out var observationId)
        && observations.TryGetValue(observationId, out var observation)
            ? observation.Provenance
            : null;

    private static VehicleConfirmationHistory MapHistory(VehicleConfirmationEntity entity)
    {
        var roleNames = JsonSerializer.Deserialize<string[]>(entity.ActorRolesJson, JsonOptions)
            ?? throw new InvalidDataException("Persisted vehicle confirmation roles are missing.");
        var roles = roleNames
            .Select(name => Enum.Parse<StaffRole>(name, ignoreCase: false))
            .ToArray();
        if (!string.Equals(entity.ActorKind, ActorKind.Staff.ToString(), StringComparison.Ordinal)
            || !Guid.TryParse(entity.ActorSubjectId, out var actorId))
        {
            throw new InvalidDataException("Persisted vehicle confirmation actor is invalid.");
        }
        VehicleMileageUnit? unit = entity.MileageUnit is null
            ? null
            : Enum.Parse<VehicleMileageUnit>(entity.MileageUnit, ignoreCase: false);
        return new(
            entity.Id,
            entity.CaseId,
            entity.LookupObservationId,
            entity.Decision,
            new(entity.Registration, entity.Make, entity.Model, entity.Mileage, unit),
            ActionActor.Staff(actorId, roles),
            entity.Reason,
            entity.OperationKey,
            entity.OccurredAtUtc,
            entity.BeforeCaseVersion,
            entity.AfterCaseVersion,
            entity.PolicyKey,
            entity.PolicyVersion);
    }

    /// <summary>
    /// One automatic-lookup sweep pass (CASE-008): every active case whose
    /// current registration (confirmed, else fact) has no lookup request yet
    /// gets one pending work item under the Automation actor. Leaseless and
    /// without a case-version bump — evidence gathering, not a staff mutation.
    /// The (CaseId, Registration) request row is the durable already-done
    /// marker, so the sweep is idempotent through success and failure alike.
    /// </summary>
    public async Task<int> EnqueueDueAsync(int maximumItems, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var terminalStates = CaseLifecycleRules.TerminalStateNames();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await context.CaseDataFields
            .AsNoTracking()
            .Where(field => field.FieldName == CaseDataFieldNames.VehicleRegistration
                && (field.ValueKind == CaseDataCodes.Confirmed || field.ValueKind == CaseDataCodes.Fact))
            .Join(
                context.CaseWorkflows.AsNoTracking()
                    .Where(workflow => workflow.ArchivedAtUtc == null
                        && !terminalStates.Contains(workflow.State)),
                field => field.CaseId,
                workflow => workflow.CaseId,
                (field, workflow) => new { field.CaseId, field.ValueKind, field.Value, workflow.Version })
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return 0;
        }

        var caseIds = candidates.Select(candidate => candidate.CaseId).Distinct().ToArray();
        var requested = (await context.Set<VehicleLookupRequestEntity>()
                .AsNoTracking()
                .Where(request => caseIds.Contains(request.CaseId))
                .Select(request => new { request.CaseId, request.Registration })
                .ToListAsync(cancellationToken))
            .Select(request => (request.CaseId, request.Registration))
            .ToHashSet();

        var enqueued = 0;
        foreach (var group in candidates.GroupBy(candidate => candidate.CaseId))
        {
            if (enqueued >= maximumItems)
            {
                break;
            }

            var registration = CurrentRegistration(
                group.Select(candidate => (candidate.ValueKind, candidate.Value)));
            if (registration is null || requested.Contains((group.Key, registration)))
            {
                continue;
            }

            try
            {
                await EnqueueAutomaticAsync(
                    group.Key,
                    registration,
                    group.First().Version,
                    cancellationToken);
                enqueued++;
            }
            catch (DbUpdateException exception) when (IsDuplicateKeyFailure(exception))
            {
                // A concurrent sweep or staff request already recorded this
                // pair; the durable marker exists, so this case is done. Any
                // other database failure (a denied permission above all)
                // propagates and fails the sweep visibly instead of counting
                // the case as already done.
            }
        }

        return enqueued;
    }

    private async Task EnqueueAutomaticAsync(
        Guid caseId,
        string registration,
        long caseVersion,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        EnqueueForCase(context, caseId, caseEntity: null, registration, caseVersion, UtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The automation client the leaseless automatic lookup acts as. Case
    /// creation and the reconciliation sweep both act as it, so the two paths
    /// compute the same operation key and the same request fingerprint for a
    /// (Case, registration) pair and cannot enqueue the same lookup twice.
    /// </summary>
    internal const string AutomationClient = "vehicle-lookup-reconciliation";

    /// <summary>
    /// The operation key both automatic paths write. Scoped by case, not just
    /// registration: <see cref="PegasusDbContext"/> enforces
    /// <c>ExternalWorkItems.OperationKey</c> as globally unique, and the same
    /// registration is routinely looked up by more than one Case.
    /// </summary>
    internal static string AutomaticOperationKey(Guid caseId, string registration) =>
        $"vehicle-lookup:auto:{caseId:N}:{registration}";

    /// <summary>
    /// The case's current registration as the automatic lookup reads it:
    /// confirmed values outrank extracted facts, and only one unambiguous
    /// normalized value is looked up. <c>null</c> means there is nothing to
    /// look up — no registration, several different ones, or text that is not a
    /// registration at all — which every caller skips silently.
    /// </summary>
    internal static string? CurrentRegistration(
        IEnumerable<(string ValueKind, string Value)> registrationFields)
    {
        var fields = registrationFields
            .Where(field => field.ValueKind is CaseDataCodes.Confirmed or CaseDataCodes.Fact)
            .ToArray();
        if (fields.Length == 0)
        {
            return null;
        }

        var tier = fields.Any(field => field.ValueKind == CaseDataCodes.Confirmed)
            ? CaseDataCodes.Confirmed
            : CaseDataCodes.Fact;
        var values = fields
            .Where(field => field.ValueKind == tier)
            .Select(field => new string(
                field.Value.ToUpperInvariant()
                    .Where(char.IsAsciiLetterOrDigit)
                    .ToArray()))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (values.Length != 1)
        {
            return null;
        }

        try
        {
            return new VehicleLookupRequest(values[0]).Registration;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Adds one leaseless automatic lookup request for a case to
    /// <paramref name="context"/> and returns the external work item to publish
    /// once the caller's transaction commits. Nothing is saved here: case
    /// creation needs these rows in the very transaction that writes the
    /// registration, so that a request can never exist without its case and a
    /// rolled-back creation leaves no orphan work.
    /// </summary>
    /// <remarks>
    /// Deliberately not an <see cref="IAutomaticVehicleLookupStore"/> port
    /// method. The port's caller is the sweep, which owns no transaction; a
    /// second store resolved beside the creation store would open its own
    /// DbContext and commit separately, which is the one thing this must not
    /// do. The sweep below and both creation stores share this writer instead,
    /// so all three produce byte-identical request rows.
    /// </remarks>
    internal static Guid EnqueueForCase(
        PegasusDbContext context,
        Guid caseId,
        CaseEntity? caseEntity,
        string registration,
        long caseVersion,
        DateTimeOffset nowUtc)
    {
        var command = new RequestVehicleLookupCommand(
            caseId,
            caseVersion,
            registration,
            ActionActor.Automation(AutomationClient),
            AutomaticOperationKey(caseId, registration),
            EditLeaseToken: "automation");
        var workItem = new ExternalWorkItemEntity
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Kind = Pegasus.Core.Custody.ExternalWorkKinds.VehicleLookup,
            OperationKey = command.OperationKey,
            State = "pending",
            AttemptCount = 0,
            DueAtUtc = nowUtc
        };
        var request = new VehicleLookupRequestEntity
        {
            WorkItemId = workItem.Id,
            WorkItem = workItem,
            CaseId = caseId,
            Registration = registration,
            OperationKey = command.OperationKey,
            RequestFingerprint = RequestFingerprint(command),
            RequestedByKind = command.Actor.Kind.ToString(),
            RequestedBySubjectId = command.Actor.SubjectId,
            RequestedByRolesJson = RolesJson(command.Actor),
            RequestedAtUtc = nowUtc,
            ResultingCaseVersion = caseVersion
        };
        if (caseEntity is not null)
        {
            // The case row is new in this same transaction, so the navigation
            // is what orders the inserts behind it.
            workItem.Case = caseEntity;
            request.Case = caseEntity;
        }

        context.ExternalWorkItems.Add(workItem);
        context.Set<VehicleLookupRequestEntity>().Add(request);
        return workItem.Id;
    }

    private static string RequestFingerprint(RequestVehicleLookupCommand command) => Hash(
        JsonSerializer.Serialize(new
        {
            command.CaseId,
            command.Registration,
            ActorKind = command.Actor.Kind.ToString(),
            command.Actor.SubjectId,
            Roles = command.Actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray(),
            command.OperationKey
        }, JsonOptions));

    private static void RequireMatchingFingerprint(
        Guid caseId,
        string operationKey,
        string persisted,
        string supplied)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(persisted),
                Convert.FromHexString(supplied)))
        {
            throw new VehicleOperationConflictException(caseId, operationKey);
        }
    }

    private static void RequireVehicleDataWritable(CaseWorkflowEntity workflow)
    {
        if (!Enum.TryParse<CaseLifecycleState>(workflow.State, ignoreCase: false, out var state)
            || !AssessmentPolicy.IsWritableState(state))
        {
            throw new InvalidOperationException(
                "Vehicle evidence is read-only in the current case state.");
        }
    }

    private static void RequireVersion(CaseWorkflowEntity workflow, long expectedVersion) =>
        CaseMutationGuard.RequireVersion(workflow, expectedVersion);

    private static void RequireLease(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string leaseToken,
        DateTimeOffset nowUtc) =>
        CaseMutationGuard.RequireLease(workflow, actor, leaseToken, nowUtc);

    private static void ClearLease(CaseWorkflowEntity workflow) =>
        CaseMutationGuard.ClearLease(workflow);

    private static void AddWorkflowEvent(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string operationKey,
        string reason,
        string requestHash,
        string eventType,
        long beforeVersion,
        long afterVersion,
        DateTimeOffset occurredAtUtc) =>
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            EventType = eventType,
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = RolesJson(actor),
            Reason = reason,
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = afterVersion
        });

    private static void AddActionHistory(
        PegasusDbContext context,
        Guid caseId,
        ActionActor actor,
        string operationKey,
        string eventKind,
        string reason,
        long beforeVersion,
        long afterVersion,
        object after,
        DateTimeOffset occurredAtUtc) =>
        context.Set<ActionHistoryEntity>().Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = caseId.ToString("D"),
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = RolesJson(actor),
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = JsonSerializer.Serialize(new { Version = beforeVersion }, JsonOptions),
            AfterJson = JsonSerializer.Serialize(new { Version = afterVersion, Value = after }, JsonOptions),
            PolicyVersion = $"{VehicleLookupFillPolicy.PolicyKey}/v{VehicleLookupFillPolicy.PolicyVersion}"
        });

    private static string RolesJson(ActionActor actor) =>
        JsonSerializer.Serialize(
            actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray(),
            JsonOptions);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool IsDuplicateKeyFailure(Exception exception) => exception switch
    {
        SqlException { Number: 2601 or 2627 } => true,
        DbUpdateException { InnerException: { } innerException } =>
            IsDuplicateKeyFailure(innerException),
        _ => false
    };

    private static bool IsRetryableConcurrencyFailure(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        DbUpdateException { InnerException: { } innerException } =>
            IsRetryableConcurrencyFailure(innerException),
        _ => false
    };
}
