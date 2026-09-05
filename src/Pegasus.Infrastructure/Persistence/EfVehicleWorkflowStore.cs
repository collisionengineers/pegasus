using System.Diagnostics;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfVehicleWorkflowStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    IEnumerable<Pegasus.Core.Intake.IProviderCaseMatchPolicy>? caseMatchPolicies = null)
    : IRequestVehicleLookupStore, IAcceptVehicleSuggestionStore, IVehicleEvidenceQueries,
        IAutomaticVehicleLookupStore
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
        if (confirmedRegistrations.Length != 1)
        {
            throw new ConfirmedVehicleRegistrationRequiredException(
                command.CaseId,
                confirmedRegistrations.Length);
        }

        var confirmedRegistration = confirmedRegistrations[0];
        if (!string.Equals(confirmedRegistration, command.Registration, StringComparison.Ordinal))
        {
            throw new ConfirmedVehicleRegistrationConflictException(
                command.CaseId,
                confirmedRegistration,
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

    public async Task<AcceptedVehicleSuggestion> AcceptAsync(
        AcceptVehicleSuggestionCommand command,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await AcceptOnceAsync(command, cancellationToken);
            }
            catch (Exception exception)
                when (attempt < 3 && IsRetryableConcurrencyFailure(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new UnreachableException();
    }

    private async Task<AcceptedVehicleSuggestion> AcceptOnceAsync(
        AcceptVehicleSuggestionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var fingerprint = AcceptanceFingerprint(command);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.Set<VehicleConfirmationEntity>()
            .AsNoTracking()
            .Include(item => item.LookupObservation)
                .ThenInclude(item => item.Request)
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
            return MapAccepted(replay, isReplay: true);
        }

        if (await OperationKeyExistsAsync(context, command.CaseId, command.OperationKey, cancellationToken))
        {
            throw new VehicleOperationConflictException(command.CaseId, command.OperationKey);
        }

        await ArchivedCaseGuard.RequireMutableAsync(context, command.CaseId, cancellationToken);
        var workflow = await context.CaseWorkflows
            .SingleAsync(item => item.CaseId == command.CaseId, cancellationToken);
        RequireVersion(workflow, command.ExpectedCaseVersion);
        var nowUtc = UtcNow();
        RequireLease(workflow, command.Actor, command.EditLeaseToken, nowUtc);

        var observationEntity = await context.Set<VehicleLookupObservationEntity>()
            .Include(item => item.Request)
            .SingleOrDefaultAsync(item => item.Id == command.LookupObservationId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Vehicle lookup observation '{command.LookupObservationId}' was not found.");
        if (observationEntity.Request.CaseId != command.CaseId)
        {
            throw new VehicleSuggestionUnavailableException(
                command.LookupObservationId,
                ParseOutcome(observationEntity.Outcome));
        }

        var observation = EfVehicleLookupWorkStore.MapObservation(observationEntity);
        var sourceIdentity = observation.Id.ToString("D");
        var selectedNames = SuggestionFieldNames(command.Field);
        var suggestionFields = await context.CaseDataFields
            .Where(item => item.CaseId == command.CaseId
                && item.ValueKind == CaseDataCodes.Suggestion
                && item.SourceIdentity == sourceIdentity
                && selectedNames.Contains(item.FieldName))
            .ToArrayAsync(cancellationToken);
        var proposedValues = ResolvePendingSuggestion(
            command.Field,
            observation,
            suggestionFields);
        if (!await context.CaseDataSnapshots
                .AnyAsync(item => item.CaseId == command.CaseId, cancellationToken))
        {
            throw new InvalidOperationException(
                "The case has no canonical accepted-data snapshot for vehicle confirmation.");
        }

        var confirmedFields = await context.CaseDataFields
            .Where(item => item.CaseId == command.CaseId
                && item.ValueKind == CaseDataCodes.Confirmed
                && VehicleFieldNames.Contains(item.FieldName))
            .ToDictionaryAsync(item => item.FieldName, StringComparer.Ordinal, cancellationToken);
        var confirmationId = Guid.NewGuid();
        var sourceLabel = suggestionFields[0].SourceLabel;
        foreach (var fieldName in selectedNames)
        {
            var (valueType, value) = fieldName switch
            {
                CaseDataFieldNames.VehicleMake => (CaseDataCodes.Text, proposedValues.Make),
                CaseDataFieldNames.VehicleModel => (CaseDataCodes.Text, proposedValues.Model),
                CaseDataFieldNames.VehicleMileage => (
                    CaseDataCodes.Integer,
                    proposedValues.Mileage?.ToString(CultureInfo.InvariantCulture)),
                CaseDataFieldNames.VehicleMileageUnit => (
                    CaseDataCodes.Text,
                    proposedValues.MileageUnit?.ToString()),
                _ => throw new UnreachableException()
            };
            SetConfirmedField(
                context,
                confirmedFields,
                command.CaseId,
                fieldName,
                valueType,
                value,
                CaseDataCodes.VehicleLookup,
                sourceIdentity,
                sourceLabel,
                command.Actor.SubjectId,
                nowUtc,
                removeWhenMissing: false);
        }
        context.CaseDataFields.RemoveRange(suggestionFields);

        // A confirmed vehicle value is the projector's preferred kind, so the case-match
        // index reprojects in this same transaction (drift here would strand the old VRM
        // as a match key). Queried rows are tracked, so updated values are visible; the
        // change tracker supplies rows this method just added or removed.
        var trackedFields = await context.CaseDataFields
            .Where(item => item.CaseId == command.CaseId)
            .ToListAsync(cancellationToken);
        var removedFields = context.ChangeTracker.Entries<CaseDataFieldEntity>()
            .Where(entry => entry.State == EntityState.Deleted
                && entry.Entity.CaseId == command.CaseId)
            .Select(entry => entry.Entity)
            .ToHashSet();
        var addedFields = context.ChangeTracker.Entries<CaseDataFieldEntity>()
            .Where(entry => entry.State == EntityState.Added
                && entry.Entity.CaseId == command.CaseId)
            .Select(entry => entry.Entity);
        var effectiveFields = trackedFields
            .Where(field => !removedFields.Contains(field))
            .Concat(addedFields)
            .ToList();
        CaseMatchIndexProjector.Apply(
            context,
            await context.CaseMatchIndex.SingleOrDefaultAsync(
                item => item.CaseId == command.CaseId,
                cancellationToken),
            CaseMatchIndexProjector.Project(
                await context.Cases.SingleAsync(
                    item => item.Id == command.CaseId,
                    cancellationToken),
                effectiveFields,
                caseMatchPolicies ?? [],
                nowUtc));

        var beforeVersion = workflow.Version;
        workflow.Version = checked(workflow.Version + 1);
        ClearLease(workflow);
        context.Set<VehicleConfirmationEntity>().Add(new()
        {
            Id = confirmationId,
            CaseId = command.CaseId,
            LookupObservationId = observation.Id,
            Decision = ToCode(command.Decision),
            Registration = proposedValues.Registration,
            Make = proposedValues.Make,
            Model = proposedValues.Model,
            Mileage = proposedValues.Mileage,
            MileageUnit = proposedValues.MileageUnit?.ToString(),
            ActorKind = command.Actor.Kind.ToString(),
            ActorSubjectId = command.Actor.SubjectId,
            ActorRolesJson = RolesJson(command.Actor),
            OperationKey = command.OperationKey,
            RequestFingerprint = fingerprint,
            Reason = command.Reason,
            OccurredAtUtc = nowUtc,
            BeforeCaseVersion = beforeVersion,
            AfterCaseVersion = workflow.Version,
            PolicyKey = VehicleSuggestionAcceptancePolicy.PolicyKey,
            PolicyVersion = VehicleSuggestionAcceptancePolicy.PolicyVersion
        });
        const string eventKind = "vehicle_suggestion_accepted";
        AddWorkflowEvent(
            context,
            workflow,
            command.Actor,
            command.OperationKey,
            command.Reason,
            fingerprint,
            eventKind,
            beforeVersion,
            workflow.Version,
            nowUtc);
        AddActionHistory(
            context,
            command.CaseId,
            command.Actor,
            command.OperationKey,
            eventKind,
            command.Reason,
            beforeVersion,
            workflow.Version,
            new
            {
                confirmationId,
                observationId = observation.Id,
                command.Field,
                command.Decision,
                Values = proposedValues
            },
            nowUtc);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            confirmationId,
            command.CaseId,
            observation.Id,
            command.Decision,
            proposedValues,
            observation.Provenance,
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

    private static string[] SuggestionFieldNames(VehicleSuggestionField field) => field switch
    {
        VehicleSuggestionField.Make => [CaseDataFieldNames.VehicleMake],
        VehicleSuggestionField.Model => [CaseDataFieldNames.VehicleModel],
        VehicleSuggestionField.Mileage =>
        [
            CaseDataFieldNames.VehicleMileage,
            CaseDataFieldNames.VehicleMileageUnit
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private static VehicleConfirmationValues ResolvePendingSuggestion(
        VehicleSuggestionField field,
        VehicleLookupObservation observation,
        IReadOnlyCollection<CaseDataFieldEntity> suggestions)
    {
        var values = suggestions.ToDictionary(item => item.FieldName, StringComparer.Ordinal);
        string Required(string fieldName) => values.TryGetValue(fieldName, out var value)
            ? value.Value
            : throw new VehicleSuggestionUnavailableException(observation.Id, observation.Outcome);

        return field switch
        {
            VehicleSuggestionField.Make => new(
                observation.Registration,
                Required(CaseDataFieldNames.VehicleMake),
                null,
                null,
                null),
            VehicleSuggestionField.Model => new(
                observation.Registration,
                null,
                Required(CaseDataFieldNames.VehicleModel),
                null,
                null),
            VehicleSuggestionField.Mileage => new(
                observation.Registration,
                null,
                null,
                long.Parse(
                    Required(CaseDataFieldNames.VehicleMileage),
                    CultureInfo.InvariantCulture),
                Enum.Parse<VehicleMileageUnit>(
                    Required(CaseDataFieldNames.VehicleMileageUnit),
                    ignoreCase: false)),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
    }

    private static void SetConfirmedField(
        PegasusDbContext context,
        Dictionary<string, CaseDataFieldEntity> fields,
        Guid caseId,
        string fieldName,
        string valueType,
        string? value,
        string sourceKind,
        string sourceIdentity,
        string sourceLabel,
        string confirmedByActor,
        DateTimeOffset confirmedAtUtc,
        bool removeWhenMissing)
    {
        if (value is null)
        {
            if (removeWhenMissing && fields.Remove(fieldName, out var removed))
            {
                context.CaseDataFields.Remove(removed);
            }
            return;
        }

        if (!fields.TryGetValue(fieldName, out var field))
        {
            field = new()
            {
                CaseId = caseId,
                FieldName = fieldName,
                ValueKind = CaseDataCodes.Confirmed,
                ValueType = valueType,
                Value = value,
                SourceKind = sourceKind,
                SourceIdentity = sourceIdentity,
                SourceLabel = sourceLabel,
                PolicyKey = VehicleSuggestionAcceptancePolicy.PolicyKey,
                PolicyVersion = VehicleSuggestionAcceptancePolicy.PolicyVersion,
                ConfirmedByActor = confirmedByActor,
                ConfirmedAtUtc = confirmedAtUtc
            };
            fields.Add(fieldName, field);
            context.CaseDataFields.Add(field);
            return;
        }

        field.ValueType = valueType;
        field.Value = value;
        field.SourceKind = sourceKind;
        field.SourceIdentity = sourceIdentity;
        field.SourceLabel = sourceLabel;
        field.PolicyKey = VehicleSuggestionAcceptancePolicy.PolicyKey;
        field.PolicyVersion = VehicleSuggestionAcceptancePolicy.PolicyVersion;
        field.ConfirmedByActor = confirmedByActor;
        field.ConfirmedAtUtc = confirmedAtUtc;
    }

    private static ConfirmedVehicleEvidence? MapConfirmed(
        Dictionary<string, CaseDataFieldEntity> fields,
        IReadOnlyDictionary<Guid, VehicleLookupObservation> observations)
    {
        if (!fields.TryGetValue(CaseDataFieldNames.VehicleRegistration, out var registration))
        {
            if (fields.Count != 0)
            {
                throw new InvalidDataException(
                    "Confirmed vehicle fields exist without a confirmed vehicle registration.");
            }
            return null;
        }

        return new(
            MapTextField(registration, observations),
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
            ParseDecision(entity.Decision),
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

    private static AcceptedVehicleSuggestion MapAccepted(
        VehicleConfirmationEntity entity,
        bool isReplay)
    {
        var observation = EfVehicleLookupWorkStore.MapObservation(entity.LookupObservation);
        VehicleMileageUnit? unit = entity.MileageUnit is null
            ? null
            : Enum.Parse<VehicleMileageUnit>(entity.MileageUnit, ignoreCase: false);
        return new(
            entity.Id,
            entity.CaseId,
            entity.LookupObservationId,
            ParseDecision(entity.Decision),
            new(entity.Registration, entity.Make, entity.Model, entity.Mileage, unit),
            observation.Provenance,
            entity.AfterCaseVersion,
            isReplay);
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

        var actor = ActionActor.Automation("vehicle-lookup-reconciliation");
        var enqueued = 0;
        foreach (var group in candidates.GroupBy(candidate => candidate.CaseId))
        {
            if (enqueued >= maximumItems)
            {
                break;
            }

            var tier = group.Any(candidate => candidate.ValueKind == CaseDataCodes.Confirmed)
                ? CaseDataCodes.Confirmed
                : CaseDataCodes.Fact;
            var values = group
                .Where(candidate => candidate.ValueKind == tier)
                .Select(candidate => new string(
                    candidate.Value.ToUpperInvariant()
                        .Where(char.IsAsciiLetterOrDigit)
                        .ToArray()))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (values.Length != 1)
            {
                continue;
            }

            VehicleLookupRequest request;
            try
            {
                request = new VehicleLookupRequest(values[0]);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (requested.Contains((group.Key, request.Registration)))
            {
                continue;
            }

            var command = new RequestVehicleLookupCommand(
                group.Key,
                group.First().Version,
                request.Registration,
                actor,
                $"vehicle-lookup:auto:{request.Registration}",
                EditLeaseToken: "automation");
            try
            {
                await EnqueueAutomaticAsync(command, cancellationToken);
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
        RequestVehicleLookupCommand command,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var nowUtc = UtcNow();
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
            RequestFingerprint = RequestFingerprint(command),
            RequestedByKind = command.Actor.Kind.ToString(),
            RequestedBySubjectId = command.Actor.SubjectId,
            RequestedByRolesJson = RolesJson(command.Actor),
            RequestedAtUtc = nowUtc,
            ResultingCaseVersion = command.ExpectedCaseVersion
        });
        await context.SaveChangesAsync(cancellationToken);
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

    private static string AcceptanceFingerprint(AcceptVehicleSuggestionCommand command) => Hash(
        JsonSerializer.Serialize(new
        {
            command.CaseId,
            command.LookupObservationId,
            command.Field,
            Decision = command.Decision.ToString(),
            command.Correction,
            ActorKind = command.Actor.Kind.ToString(),
            command.Actor.SubjectId,
            Roles = command.Actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray(),
            command.OperationKey,
            command.Reason
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
            PolicyVersion = $"{VehicleSuggestionAcceptancePolicy.PolicyKey}/v{VehicleSuggestionAcceptancePolicy.PolicyVersion}"
        });

    private static string RolesJson(ActionActor actor) =>
        JsonSerializer.Serialize(
            actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray(),
            JsonOptions);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string ToCode(VehicleSuggestionDecision decision) => decision switch
    {
        VehicleSuggestionDecision.Accept => "accepted",
        _ => throw new ArgumentOutOfRangeException(nameof(decision))
    };

    private static VehicleSuggestionDecision ParseDecision(string decision) => decision switch
    {
        "accepted" => VehicleSuggestionDecision.Accept,
        _ => throw new InvalidDataException(
            $"Persisted vehicle suggestion decision '{decision}' is invalid.")
    };

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

    private static VehicleLookupOutcome ParseOutcome(string outcome) => outcome switch
    {
        "current" => VehicleLookupOutcome.Current,
        "stale" => VehicleLookupOutcome.Stale,
        "partial" => VehicleLookupOutcome.Partial,
        "not_found" => VehicleLookupOutcome.NotFound,
        "throttled" => VehicleLookupOutcome.Throttled,
        "unavailable" => VehicleLookupOutcome.Unavailable,
        "error" => VehicleLookupOutcome.Failed,
        _ => throw new InvalidDataException(
            $"Persisted vehicle lookup outcome '{outcome}' is invalid.")
    };
}
