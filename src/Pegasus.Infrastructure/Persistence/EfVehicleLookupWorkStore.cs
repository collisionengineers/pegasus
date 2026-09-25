using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfVehicleLookupWorkStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IVehicleLookupWorkStore
{
    private const int MotJsonVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<VehicleLookupWorkItem?> ClaimProcessingAsync(
        Guid workItemId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "A vehicle lookup work item identifier is required.",
                nameof(workItemId));
        }
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

        var leaseToken = Guid.NewGuid().ToString("N");
        while (true)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var work = await context.ExternalWorkItems
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == workItemId, cancellationToken)
                ?? throw new InvalidOperationException("The vehicle lookup work item is unavailable.");
            if (!string.Equals(work.Kind, Pegasus.Core.Custody.ExternalWorkKinds.VehicleLookup, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The external work item is not a vehicle lookup.");
            }
            if (work.State is "completed" or "failed")
            {
                return null;
            }
            if (work.State == "pending" && work.DueAtUtc > nowUtc)
            {
                return null;
            }
            if (work.State == "processing" && work.LeaseExpiresAtUtc > nowUtc)
            {
                throw new InvalidOperationException("The vehicle lookup work item is already leased.");
            }
            if (work.State is not ("pending" or "dispatching" or "queued" or "processing"))
            {
                throw new InvalidDataException(
                    $"The vehicle lookup work item has unknown state '{work.State}'.");
            }

            var request = await context.Set<VehicleLookupRequestEntity>()
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.WorkItemId == workItemId, cancellationToken)
                ?? throw new InvalidDataException(
                    "The vehicle lookup work item has no immutable request payload.");
            var leaseExpiresAtUtc = nowUtc.Add(leaseDuration);
            var claimed = await context.ExternalWorkItems
                .Where(item => item.Id == work.Id
                    && item.State == work.State
                    && item.AttemptCount == work.AttemptCount
                    && item.LeaseToken == work.LeaseToken
                    && item.LeaseExpiresAtUtc == work.LeaseExpiresAtUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, "processing")
                    .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1)
                    .SetProperty(item => item.LeaseToken, leaseToken)
                    .SetProperty(item => item.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(item => item.FailureCode, (string?)null)
                    .SetProperty(item => item.FailureReason, (string?)null),
                    cancellationToken);
            if (claimed == 1)
            {
                return new(
                    work.Id,
                    work.CaseId!.Value,
                    request.Registration,
                    request.OperationKey,
                    VehicleLookupWorkState.Processing,
                    checked(work.AttemptCount + 1),
                    work.DueAtUtc,
                    leaseToken,
                    leaseExpiresAtUtc);
            }
        }
    }

    public async Task RecordOutcomeAsync(
        Guid workItemId,
        string leaseToken,
        VehicleLookupProcessedOutcome outcome,
        VehicleLookupWorkState state,
        DateTimeOffset? dueAtUtc,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "A vehicle lookup work item identifier is required.",
                nameof(workItemId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseToken);
        ArgumentNullException.ThrowIfNull(outcome);
        if (state is not (
            VehicleLookupWorkState.Completed or
            VehicleLookupWorkState.Failed or
            VehicleLookupWorkState.RetryScheduled))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
        if ((state == VehicleLookupWorkState.RetryScheduled) != dueAtUtc.HasValue)
        {
            throw new ArgumentException(
                "Only retry-scheduled vehicle work requires a due time.",
                nameof(dueAtUtc));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var work = await context.ExternalWorkItems
            .SingleOrDefaultAsync(item => item.Id == workItemId, cancellationToken)
            ?? throw new InvalidOperationException("The vehicle lookup work item is unavailable.");
        if (!string.Equals(work.Kind, Pegasus.Core.Custody.ExternalWorkKinds.VehicleLookup, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The external work item is not a vehicle lookup.");
        }
        if (work.State is "completed" or "failed")
        {
            return;
        }
        if (!string.Equals(work.State, "processing", StringComparison.Ordinal)
            || !string.Equals(work.LeaseToken, leaseToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The vehicle lookup processing lease was lost before its outcome was recorded.");
        }

        var request = await context.Set<VehicleLookupRequestEntity>()
            .SingleAsync(item => item.WorkItemId == workItemId, cancellationToken);
        var workflow = await context.CaseWorkflows
            .SingleAsync(item => item.CaseId == work.CaseId, cancellationToken);
        var beforeCaseVersion = workflow.Version;
        CaseMutationGuard.Complete(workflow);
        var lookupRequest = new VehicleLookupRequest(request.Registration);
        outcome.Result.EnsureValidFor(lookupRequest);
        var expectedMileage = VehicleMileagePolicy.Calculate(outcome.Result.MotTests);
        if (outcome.Mileage != expectedMileage)
        {
            throw new InvalidDataException(
                "The vehicle mileage calculation does not match the accepted deterministic policy.");
        }

        var result = outcome.Result;
        var observationId = Guid.NewGuid();
        context.Set<VehicleLookupObservationEntity>().Add(new()
        {
            Id = observationId,
            WorkItemId = workItemId,
            AttemptNumber = work.AttemptCount,
            Outcome = ToCode(result.Outcome),
            Registration = result.Registration,
            Provider = result.Provider,
            ProviderVersion = result.ProviderVersion,
            ResponseIdentity = result.ResponseIdentity,
            RetrievedAtUtc = result.RetrievedAtUtc,
            EffectiveAtUtc = result.EffectiveAtUtc,
            SourceObservedAtUtc = result.SourceObservedAtUtc,
            Make = result.Vehicle?.Make,
            Model = result.Vehicle?.Model,
            ManufactureYear = result.Vehicle?.ManufactureYear,
            EngineCapacityCc = result.Vehicle?.EngineCapacityCc,
            FuelType = result.Vehicle?.FuelType,
            TypeApproval = result.Vehicle?.TypeApproval,
            Wheelplan = result.Vehicle?.Wheelplan,
            RevenueWeightKg = result.Vehicle?.RevenueWeightKg,
            Colour = result.Vehicle?.Colour,
            TaxDueDate = result.Vehicle?.TaxDueDate,
            MotTestsJson = SerializeMotTests(result.MotTests),
            MileageValue = outcome.Mileage?.Value,
            MileageUnit = outcome.Mileage?.Unit.ToString(),
            MileageObservedOn = outcome.Mileage?.ObservedOn,
            MileageMethodKey = outcome.Mileage?.MethodKey,
            MileageMethodVersion = outcome.Mileage?.MethodVersion,
            MileageSupportingObservationCount = outcome.Mileage?.SupportingObservationCount,
            FailureCode = result.Failure?.Code,
            FailureRetryable = result.Failure?.Retryable,
            FailureRetryAfterTicks = result.Failure?.RetryAfter?.Ticks,
            RecordedAtUtc = recordedAtUtc
        });

        // Looked-up values fill the current work's empty fields when the answer
        // is for the registration that work now names.
        var workId = await CaseWorkScope.CurrentIdAsync(context, workflow.CaseId, cancellationToken);
        var caseDataFields = await context.CaseDataFields
            .Where(item => item.WorkId == workId)
            .ToListAsync(cancellationToken);
        var selectedMileageSource = await context.CaseAssessmentFields
            .Where(item => item.WorkId == workId
                && item.FieldPath == Pegasus.Core.Assessment.AssessmentVocabulary.VehicleMileageSource)
            .Select(item => item.Value)
            .SingleOrDefaultAsync(cancellationToken);
        var beforeVehicle = ReportVehicleDependencies(caseDataFields, selectedMileageSource);
        var (beforeAssessment, afterAssessment) = await FillVehicleFieldsAsync(
            context, caseDataFields, workId, observationId, result, outcome.Mileage, recordedAtUtc, cancellationToken);
        var freshness = CaseReportFreshness.ClassifyVehicle(
            beforeVehicle,
            ReportVehicleDependencies(caseDataFields, selectedMileageSource));
        // The Vehicle type and the lookup's own facts are printed assessment facts,
        // not Case-data vehicle dependencies: the assessment classifier decides.
        if (!freshness.IsStale)
        {
            freshness = CaseReportFreshness.ClassifyAssessment(beforeAssessment, afterAssessment);
        }
        if (freshness.IsStale)
        {
            await EfCaseReportGenerationStore.MarkStaleAsync(
                context,
                workflow.CaseId,
                freshness.ReasonCode!,
                recordedAtUtc,
                cancellationToken);
        }

        work.State = state switch
        {
            VehicleLookupWorkState.RetryScheduled => "pending",
            VehicleLookupWorkState.Completed => "completed",
            VehicleLookupWorkState.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
        work.DueAtUtc = dueAtUtc ?? recordedAtUtc;
        work.CompletedAtUtc = state == VehicleLookupWorkState.Completed ? recordedAtUtc : null;
        work.ExternalReceipt = result.ResponseIdentity;
        work.FailureCode = result.Failure?.Code;
        work.FailureReason = result.Failure is null
            ? null
            : $"Vehicle lookup returned the typed outcome '{ToCode(result.Outcome)}'.";
        work.LeaseToken = null;
        work.LeaseExpiresAtUtc = null;

        var outcomeHistory = new VehicleOutcomeHistory(
            observationId,
            workItemId,
            work.AttemptCount,
            ToCode(result.Outcome));
        var resultJson = JsonSerializer.Serialize(outcomeHistory, JsonOptions);
        var outcomeOperationKey = OutcomeOperationKey(workItemId, work.AttemptCount);
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = $"vehicle_lookup_{ToCode(result.Outcome)}",
            OperationKey = outcomeOperationKey,
            RequestHash = Hash(resultJson),
            ActorKind = ActorKind.SystemWorker.ToString(),
            ActorSubjectId = "vehicle-lookup",
            ActorRolesJson = "[]",
            Reason = result.Failure is null
                ? "Vehicle lookup outcome recorded."
                : $"Vehicle lookup outcome recorded with failure code '{result.Failure.Code}'.",
            OccurredAtUtc = recordedAtUtc,
            BeforeVersion = beforeCaseVersion,
            AfterVersion = workflow.Version,
            ResultJson = resultJson
        });

        context.Set<ActionHistoryEntity>().Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = workflow.CaseId.ToString("D"),
            EventKind = $"vehicle_lookup_{ToCode(result.Outcome)}",
            ActorKind = "SystemWorker",
            ActorSubjectId = "vehicle-lookup",
            ActorRolesJson = "[]",
            OccurredAtUtc = recordedAtUtc,
            Outcome = state switch
            {
                VehicleLookupWorkState.RetryScheduled => "RetryScheduled",
                VehicleLookupWorkState.Completed => "Succeeded",
                VehicleLookupWorkState.Failed => "Failed",
                _ => throw new ArgumentOutOfRangeException(nameof(state))
            },
            CorrelationId = request.OperationKey,
            Reason = result.Failure?.Code,
            BeforeJson = null,
            AfterJson = resultJson,
            PolicyVersion = $"{VehicleMileagePolicy.MethodKey}/v{VehicleMileagePolicy.MethodVersion}"
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// The lookup is enrichment, so what it learns fills the case's
    /// own empty vehicle fields instead of sitting beside them as a rival
    /// reading. A filled value is the case's working value from the moment it
    /// lands, carrying Lookup provenance so the report can still say where the
    /// figure came from.
    ///
    /// <see cref="Pegasus.Core.Vehicle.VehicleLookupFillPolicy.Fills"/> is the
    /// rule for the Case-data fields and the Vehicle type: it never overwrites
    /// an extracted fact or a staff value, and a field the case already
    /// answers is left alone. The facts only the lookup records
    /// (<see cref="AssessmentVocabulary.LookupDerivedPaths"/>) follow each
    /// answer as
    /// <see cref="Pegasus.Core.Vehicle.VehicleLookupFillPolicy.DerivedAssessmentWrites"/>
    /// says, recorded confirmed by the lookup. All of it applies only when the
    /// answer is for the work's current registration
    /// (<see cref="EfVehicleWorkflowStore.CurrentRegistration"/>). Returns the
    /// lookup-written assessment values before and after this answer, for
    /// report freshness.
    ///
    /// Runs inside the caller's transaction, alongside the observation it
    /// came from, so the two can never disagree about what the lookup said.
    /// </summary>
    private static async Task<(Dictionary<string, string?> Before, Dictionary<string, string?> After)> FillVehicleFieldsAsync(
        PegasusDbContext context,
        List<CaseDataFieldEntity> caseDataFields,
        Guid workId,
        Guid observationId,
        VehicleLookupResult result,
        VehicleMileageCalculation? mileage,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken)
    {
        // An answer describes the vehicle it looked up. One for a registration
        // the work no longer names (staff corrected it while the lookup was
        // queued or retrying) records its observation and changes nothing on
        // the work, so it can never replace or clear the current vehicle's facts.
        var currentRegistration = EfVehicleWorkflowStore.CurrentRegistration(
            caseDataFields
                .Where(item => item.FieldName == CaseDataFieldNames.VehicleRegistration)
                .Select(item => (item.ValueKind, item.Value)));
        if (!string.Equals(currentRegistration, result.Registration, StringComparison.Ordinal))
        {
            var unchanged = new Dictionary<string, string?>(StringComparer.Ordinal);
            return (unchanged, unchanged);
        }

        var answered = caseDataFields
            .Where(item => item.ValueKind is CaseDataCodes.Fact or CaseDataCodes.Confirmed)
            .ToArray();

        // Suggestion rows are no longer written, but an estate case looked up
        // before this change can still carry them. Clearing this case's
        // lookup-sourced suggestions leaves no orphan behind a value that now
        // outranks it, and re-running a lookup cannot accumulate two.
        var staleSuggestions = caseDataFields
            .Where(item => item.ValueKind == CaseDataCodes.Suggestion
                && item.SourceKind == CaseDataCodes.VehicleLookup)
            .ToArray();
        context.CaseDataFields.RemoveRange(staleSuggestions);
        foreach (var staleSuggestion in staleSuggestions)
        {
            caseDataFields.Remove(staleSuggestion);
        }

        var sourceIdentity = observationId.ToString("D");
        var sourceLabel = $"{result.Provider}/{result.ProviderVersion}";

        void Fill(string fieldName, string valueType, string? value, string policyKey, int policyVersion)
        {
            var hasFact = answered.Any(item =>
                item.FieldName == fieldName && item.ValueKind == CaseDataCodes.Fact);
            var hasStaffValue = answered.Any(item =>
                item.FieldName == fieldName && item.ValueKind == CaseDataCodes.Confirmed);
            if (string.IsNullOrWhiteSpace(value)
                || !VehicleLookupFillPolicy.Fills(hasFact, hasStaffValue))
            {
                return;
            }

            var field = new CaseDataFieldEntity
            {
                WorkId = workId,
                FieldName = fieldName,
                ValueKind = CaseDataCodes.Fact,
                ValueType = valueType,
                Value = value,
                SourceKind = CaseDataCodes.VehicleLookup,
                SourceIdentity = sourceIdentity,
                SourceLabel = sourceLabel,
                PolicyKey = policyKey,
                PolicyVersion = policyVersion
            };
            context.CaseDataFields.Add(field);
            caseDataFields.Add(field);
        }

        Fill(
            CaseDataFieldNames.VehicleMake,
            CaseDataCodes.Text,
            result.Vehicle?.Make,
            VehicleLookupFillPolicy.PolicyKey,
            VehicleLookupFillPolicy.PolicyVersion);
        Fill(
            CaseDataFieldNames.VehicleModel,
            CaseDataCodes.Text,
            result.Vehicle?.Model,
            VehicleLookupFillPolicy.PolicyKey,
            VehicleLookupFillPolicy.PolicyVersion);
        Fill(
            CaseDataFieldNames.VehicleYear,
            CaseDataCodes.Text,
            result.Vehicle?.ManufactureYear?.ToString(CultureInfo.InvariantCulture),
            VehicleLookupFillPolicy.PolicyKey,
            VehicleLookupFillPolicy.PolicyVersion);
        if (mileage is { } derived)
        {
            // The mileage carries the calculation's own key and version, not
            // this rule's, because that is what classifies it as a derived
            // estimate everywhere it is later shown.
            Fill(
                CaseDataFieldNames.VehicleMileage,
                CaseDataCodes.Integer,
                derived.Value.ToString(CultureInfo.InvariantCulture),
                VehicleMileagePolicy.MethodKey,
                derived.MethodVersion);
            Fill(
                CaseDataFieldNames.VehicleMileageUnit,
                CaseDataCodes.Text,
                derived.Unit.ToString(),
                VehicleMileagePolicy.MethodKey,
                derived.MethodVersion);
        }

        // The Vehicle type keeps its fill rule; the facts only the lookup records
        // follow each answer. Both are read once, so report freshness compares
        // their values before and after this answer.
        string[] assessmentPaths = [AssessmentVocabulary.VehicleType, .. AssessmentVocabulary.LookupDerivedPaths];
        var assessmentRows = await context.CaseAssessmentFields
            .Where(item => item.WorkId == workId && assessmentPaths.Contains(item.FieldPath))
            .ToListAsync(cancellationToken);
        var before = Values(assessmentRows);

        void Write(CaseAssessmentFieldEntity? existing, string path, string value)
        {
            var written = AssessmentFieldWriter.Write(
                context, workId, existing, path, value,
                ActorKind.Automation, VehicleLookupFillPolicy.RecorderId, recordedAtUtc);
            if (existing is null)
            {
                assessmentRows.Add(written);
            }
        }

        // Vehicle type: a staff-editable value, filled only where staff have
        // not recorded one, re-stamped only when it changes.
        var vehicleType = VehicleTypePolicy.Classify(result.Vehicle);
        var existingType = assessmentRows.SingleOrDefault(item => item.FieldPath == AssessmentVocabulary.VehicleType);
        if (vehicleType is not null
            && VehicleLookupFillPolicy.Fills(hasFact: false, hasStaffValue: AssessmentFieldWriter.IsStaffRecorded(existingType))
            && !string.Equals(existingType?.Value, vehicleType, StringComparison.Ordinal))
        {
            Write(existingType, AssessmentVocabulary.VehicleType, vehicleType);
        }

        // The facts only the lookup records are its to replace and, on a complete
        // answer, to clear: recorded by the lookup whatever wrote the row before;
        // an unchanged value the lookup recorded is left as it stands.
        foreach (var (path, value) in VehicleLookupFillPolicy.DerivedAssessmentWrites(result))
        {
            var existing = assessmentRows.SingleOrDefault(item => item.FieldPath == path);
            if (value is null)
            {
                if (existing is not null)
                {
                    context.CaseAssessmentFields.Remove(existing);
                    assessmentRows.Remove(existing);
                }
            }
            else if (existing is not { RecordedBy: VehicleLookupFillPolicy.RecorderId }
                || !string.Equals(existing.Value, value, StringComparison.Ordinal))
            {
                Write(existing, path, value);
            }
        }

        return (before, Values(assessmentRows));

        static Dictionary<string, string?> Values(IEnumerable<CaseAssessmentFieldEntity> rows) =>
            rows.ToDictionary(item => item.FieldPath, item => (string?)item.Value, StringComparer.Ordinal);
    }

    private static CaseReportVehicleDependencies ReportVehicleDependencies(
        IReadOnlyList<CaseDataFieldEntity> fields,
        string? selectedMileageSource)
    {
        string? Current(string fieldName) => CaseDataFieldValues.Current(fields, fieldName);
        var mileage = CaseDataFieldValues.CurrentField(fields, CaseDataFieldNames.VehicleMileage);
        return new(
            Current(CaseDataFieldNames.VehicleMake),
            Current(CaseDataFieldNames.VehicleModel),
            Current(CaseDataFieldNames.VehicleYear),
            mileage is null
                ? null
                : long.Parse(mileage.Value, NumberStyles.None, CultureInfo.InvariantCulture),
            Current(CaseDataFieldNames.VehicleMileageUnit),
            CaseVehicleMileageSourcePolicy.Resolve(
                mileage is null ? null : EfCaseDataStore.ParseSourceKind(mileage.SourceKind),
                mileage is not null,
                selectedMileageSource));
    }

    internal static VehicleLookupObservation MapObservation(VehicleLookupObservationEntity entity)
    {
        var motTests = DeserializeMotTests(entity.MotTestsJson);
        var mileage = entity.MileageValue is { } mileageValue
            && entity.MileageUnit is { } mileageUnit
            && entity.MileageObservedOn is { } observedOn
            && entity.MileageMethodKey is { } methodKey
            && entity.MileageMethodVersion is { } methodVersion
            && entity.MileageSupportingObservationCount is { } supportingCount
                ? new VehicleMileageCalculation(
                    mileageValue,
                    Enum.Parse<VehicleMileageUnit>(mileageUnit, ignoreCase: false),
                    observedOn,
                    methodKey,
                    methodVersion,
                    supportingCount)
                : null;
        var failure = entity.FailureCode is null
            ? null
            : new VehicleLookupFailure(
                entity.FailureCode,
                entity.FailureRetryable
                    ?? throw new InvalidDataException("Persisted vehicle failure retryability is missing."),
                entity.FailureRetryAfterTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null);
        return new(
            entity.Id,
            entity.WorkItemId,
            entity.Request.CaseId,
            entity.AttemptNumber,
            ParseOutcome(entity.Outcome),
            entity.Registration,
            new(
                entity.Provider,
                entity.ProviderVersion,
                entity.ResponseIdentity,
                entity.RetrievedAtUtc,
                entity.EffectiveAtUtc,
                entity.SourceObservedAtUtc),
            entity.Make is null
                && entity.Model is null
                && entity.ManufactureYear is null
                && entity.EngineCapacityCc is null
                && entity.FuelType is null
                && entity.TypeApproval is null
                && entity.Wheelplan is null
                && entity.RevenueWeightKg is null
                && entity.Colour is null
                && entity.TaxDueDate is null
                    ? null
                    : new(
                        entity.Make,
                        entity.Model,
                        entity.ManufactureYear,
                        entity.EngineCapacityCc,
                        entity.FuelType,
                        entity.TypeApproval,
                        entity.Wheelplan,
                        entity.RevenueWeightKg,
                        entity.Colour,
                        entity.TaxDueDate),
            motTests,
            mileage,
            failure,
            entity.RecordedAtUtc);
    }

    internal static VehicleLookupWorkState MapWorkState(ExternalWorkItemEntity work) =>
        work.State switch
        {
            "pending" when work.AttemptCount > 0 => VehicleLookupWorkState.RetryScheduled,
            "pending" or "dispatching" or "queued" => VehicleLookupWorkState.Pending,
            "processing" => VehicleLookupWorkState.Processing,
            "completed" => VehicleLookupWorkState.Completed,
            "failed" => VehicleLookupWorkState.Failed,
            _ => throw new InvalidDataException(
                $"The persisted vehicle lookup state '{work.State}' is invalid.")
        };

    private static string SerializeMotTests(IReadOnlyList<MotTestObservation> observations)
    {
        var ordered = observations
            .OrderBy(item => item.TestDate)
            .ThenBy(item => item.TestStatus, StringComparer.Ordinal)
            .ThenBy(item => item.ExpiryDate)
            .ThenBy(item => item.Mileage)
            .ThenBy(item => item.MileageUnit)
            .ToArray();
        return JsonSerializer.Serialize(new MotTestEnvelope(MotJsonVersion, ordered), JsonOptions);
    }

    private static MotTestObservation[] DeserializeMotTests(string json)
    {
        var envelope = JsonSerializer.Deserialize<MotTestEnvelope>(json, JsonOptions)
            ?? throw new InvalidDataException("Persisted MOT evidence is missing.");
        if (envelope.Version != MotJsonVersion || envelope.Observations is null)
        {
            throw new InvalidDataException("Persisted MOT evidence has an unsupported version.");
        }
        return envelope.Observations;
    }

    private static string ToCode(VehicleLookupOutcome outcome) => outcome switch
    {
        VehicleLookupOutcome.Current => "current",
        VehicleLookupOutcome.Stale => "stale",
        VehicleLookupOutcome.Partial => "partial",
        VehicleLookupOutcome.NotFound => "not_found",
        VehicleLookupOutcome.Throttled => "throttled",
        VehicleLookupOutcome.Unavailable => "unavailable",
        VehicleLookupOutcome.Failed => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
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
        _ => throw new InvalidDataException($"Persisted vehicle outcome '{outcome}' is invalid.")
    };

    private static string OutcomeOperationKey(Guid workItemId, int attemptNumber) =>
        $"vehicle-outcome:{workItemId:D}:{attemptNumber.ToString(CultureInfo.InvariantCulture)}";

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private sealed record MotTestEnvelope(int Version, MotTestObservation[] Observations);

    private sealed record VehicleOutcomeHistory(
        Guid ObservationId,
        Guid WorkItemId,
        int AttemptNumber,
        string Outcome);
}
