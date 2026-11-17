using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfCaseDataStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : ICaseDataQueries
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CaseDataProjection?> GetAsync(
        Guid caseId,
        CaseWorkSelector work,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // The selected work is resolved inside the snapshot read.
        var selectedWorkIds = CaseWorkScope.SelectedIds(context, caseId, work);
        // The snapshot, the workflow's version and state and the workflow
        // configuration the completeness is evaluated under are one command.
        var row = await SnapshotQuery(context, tracking: false)
            .Where(item => selectedWorkIds.Contains(item.WorkId))
            .Select(item => new
            {
                Snapshot = item,
                Workflow = context.CaseWorkflows
                    .Where(workflow => workflow.CaseId == caseId)
                    .Select(workflow => new { workflow.Version, workflow.State })
                    .FirstOrDefault(),
                Configuration = context.Set<WorkflowConfigurationEntity>()
                    .FirstOrDefault(configuration =>
                        configuration.Id == AdministrationPolicyModelConfiguration.WorkflowPolicyKey)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var workflow = row.Workflow
            ?? throw new InvalidDataException(
                "The accepted case data snapshot has no workflow record.");
        var configuration = row.Configuration
            ?? throw new InvalidOperationException(
                "The current workflow configuration has not been initialized.");
        return ApplyConfiguration(
            Map(row.Snapshot, workflow.Version, workflow.State),
            EfWorkflowConfigurationStore.Map(configuration));
    }

    internal static CaseDataProjection ApplyConfiguration(CaseDataProjection data, CaseWorkflowConfiguration configuration) =>
        data with
        {
            AiDraftTargetDays = configuration.AiDraftTargetDays,
            Completeness = data.Completeness with
            {
                // The accepted registration and mileage, as the stores' gate
                // reads them (R759, R1081).
                Evaluation = CaseCompletenessPolicy.Evaluate(
                    data.Completeness.Values,
                    configuration,
                    CaseCompletenessPolicy.VehicleGate(
                        data.State,
                        (data.Vehicle.Registration.Confirmed ?? data.Vehicle.Registration.Fact)?.Value,
                        (data.Vehicle.Mileage.Confirmed ?? data.Vehicle.Mileage.Fact)?.Value))
            }
        };

    internal static IQueryable<CaseDataSnapshotEntity> SnapshotQuery(
        PegasusDbContext context,
        bool tracking)
    {
        var query = context.CaseDataSnapshots
            .Include(item => item.Work)
            .ThenInclude(item => item.Case)
            .ThenInclude(item => item.Principal)
            .Include(item => item.Fields);
        return tracking ? query : query.AsNoTracking();
    }

    internal static CaseDataProjection Map(
        CaseDataSnapshotEntity snapshot,
        CaseWorkflowEntity workflow) => Map(snapshot, workflow.Version, workflow.State);

    /// <summary>The projection of a snapshot at the Case workflow's version and state.</summary>
    internal static CaseDataProjection Map(
        CaseDataSnapshotEntity snapshot,
        long workflowVersion,
        string workflowState) => new(
        new(
            snapshot.Work.CaseId,
            snapshot.Work.Case.Principal.Code,
            snapshot.Work.Case.Year,
            snapshot.Work.Case.Sequence,
            snapshot.Work.Case.Reference,
            snapshot.Work.Case.AuditReference),
        new(
            snapshot.OriginIntakeReceiptId,
            snapshot.OriginSourceChannel is null
                ? null
                : EfIntakeReceiptStore.ParseSourceChannel(snapshot.OriginSourceChannel),
            snapshot.OriginExternalReceiptToken,
            snapshot.OriginSourceHash,
            snapshot.OriginReceivedAtUtc,
            snapshot.SourceReaderKey,
            snapshot.SourceReaderVersion,
            snapshot.ExtractionPolicyKey,
            snapshot.ExtractionPolicyVersion),
        snapshot.AcceptedAtUtc,
        workflowVersion,
        ParseLifecycleState(workflowState),
        new(
            new(
                snapshot.Work.Case.InstructionComplete,
                snapshot.Work.Case.ImagesComplete),
            new(
                snapshot.CompletenessPolicySatisfied,
                snapshot.CompletenessPolicyKey,
                snapshot.CompletenessPolicyVersion))
        {
            MissingInstructionItems = snapshot.Work.Case.InstructionComplete
                ? []
                : CaseInstructionCompleteness.Missing(snapshot)
        },
        new(TextField(snapshot, CaseDataFieldNames.PrincipalCode)),
        new(
            TextField(snapshot, CaseDataFieldNames.ClaimantName),
            TextField(snapshot, CaseDataFieldNames.ClaimantContactNumber),
            TextField(snapshot, CaseDataFieldNames.ClaimantAddress)),
        new(TextField(snapshot, CaseDataFieldNames.ClaimNumber)),
        new CaseVehicleData(
            TextField(snapshot, CaseDataFieldNames.VehicleRegistration),
            TextField(snapshot, CaseDataFieldNames.VehicleMake),
            TextField(snapshot, CaseDataFieldNames.VehicleModel),
            TextField(snapshot, CaseDataFieldNames.VehicleYear),
            LongField(snapshot, CaseDataFieldNames.VehicleMileage),
            TextField(snapshot, CaseDataFieldNames.VehicleMileageUnit))
        {
            Description = TextField(snapshot, CaseDataFieldNames.VehicleDescription)
        },
        new(
            DateField(snapshot, CaseDataFieldNames.IncidentDate),
            TextField(snapshot, CaseDataFieldNames.AccidentCircumstances)),
        new(
            CaseDataPolicy.ReceivedDate(snapshot.OriginReceivedAtUtc, snapshot.Work.Case.CreatedAtUtc),
            TextField(snapshot, CaseDataFieldNames.VatStatus)),
        new(
            DateField(snapshot, CaseDataFieldNames.InspectionDate),
            DateField(snapshot, CaseDataFieldNames.InspectionDeadline),
            TextField(snapshot, CaseDataFieldNames.InspectionAddress),
            InspectionModeField(snapshot, CaseDataFieldNames.InspectionMode),
            TextField(snapshot, CaseDataFieldNames.StorageLocation),
            TextField(snapshot, CaseDataFieldNames.RepairerAddress),
            TextField(snapshot, CaseDataFieldNames.RepairerName)),
        Workspace(snapshot),
        snapshot.Work.Case.StandaloneAuditEvidenceId);

    /// <summary>
    /// The v1 workspace facts. Each is entered by staff through the one Case
    /// save, so the current value is the whole story and the projection carries
    /// the value itself rather than a fact/suggestion/confirmed triple.
    /// </summary>
    private static CaseWorkspaceData Workspace(CaseDataSnapshotEntity snapshot)
    {
        var data = CaseDataFieldWriter.ReadEditable(snapshot);
        return new(
            data.ClaimSourceId is not null
                ? new(
                    data.ClaimSourceId,
                    data.ClaimSourceVersion,
                    data.ClaimSourceName,
                    data.ClaimSourceContactName,
                    data.ClaimSourceContactTelephone,
                    data.ClaimSourceContactEmailAddress,
                    data.ClaimSourceOverrideContactName,
                    data.ClaimSourceOverrideContactTelephone,
                    data.ClaimSourceOverrideContactEmailAddress)
                : null,
            data.InspectionAddressTreatment,
            new(
                data.InspectionLocationChoice,
                data.InspectionLocationSource,
                data.InspectionLocationSourceId,
                data.InspectionLocationSourceVersion,
                data.InspectionLocationSourceLabel),
            data.InspectionVehiclePresent,
            data.InspectionCondition,
            data.InspectionNotes,
            data.VehicleMileageDisplayUnit is { } displayUnit
                && CaseOdometer.TryParseUnit(displayUnit, out var unit)
                    ? unit
                    : null,
            new(data.RepairerId, data.RepairerVersion, data.RepairerName),
            data.PrincipalNotes,
            data.ClaimSourceNotes);
    }

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
        string fieldName) => Field(snapshot, fieldName, CaseDataFieldWriter.ParseInspectionMode);

    private static CaseField<T> Field<T>(
        CaseDataSnapshotEntity snapshot,
        string fieldName,
        Func<string, T> parse)
        where T : notnull
    {
        var values = snapshot.Fields.Where(item => item.FieldName == fieldName).ToArray();
        if (CaseDataFieldValues.IsClearedField(values, fieldName))
        {
            // A box staff emptied has no value, and the fact or suggestion
            // behind it is not shown (R1077).
            return new(null, null, null) { Cleared = true };
        }

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
            Source(value),
            value.ConfirmedByActor,
            value.ConfirmedAtUtc);
    }

    /// <summary>Where a persisted case-data row came from.</summary>
    internal static CaseDataSource Source(CaseDataFieldEntity field) =>
        new(
            ParseSourceKind(field.SourceKind),
            field.SourceIdentity,
            field.SourceLabel,
            field.PolicyKey,
            field.PolicyVersion);

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

    /// <summary>
    /// The one reader of a persisted source-kind code. Shared so that a second
    /// reader of the same column — the assessment projection derives the
    /// report's mileage source from it — cannot spell the mapping differently.
    /// </summary>
    internal static CaseDataSourceKind ParseSourceKind(string value) => value switch
    {
        CaseDataCodes.IntakeEvidence => CaseDataSourceKind.IntakeEvidence,
        CaseDataCodes.MailRoute => CaseDataSourceKind.MailRoute,
        CaseDataCodes.CaseAcceptance => CaseDataSourceKind.CaseAcceptance,
        CaseDataCodes.StaffCorrection => CaseDataSourceKind.StaffCorrection,
        CaseDataCodes.VehicleLookup => CaseDataSourceKind.VehicleLookup,
        CaseDataCodes.PrincipalSetting => CaseDataSourceKind.PrincipalSetting,
        CaseDataCodes.PrincipalApi => CaseDataSourceKind.PrincipalApi,
        CaseDataCodes.PhotoLink => CaseDataSourceKind.PhotoLink,
        _ => throw new InvalidDataException(
            $"Unknown persisted case-data source kind '{value}'.")
    };
}

/// <summary>
/// The one reader and writer of the confirmed case-data field rows behind
/// <see cref="CaseEditableData"/>. Both the case-detail save and the Case
/// workspace save go through it, so a fact has one persisted name, one value
/// type and one provenance rule whichever command wrote it.
/// </summary>
internal static class CaseDataFieldWriter
{
    public static void ApplyEditableData(
        PegasusDbContext context,
        CaseDataSnapshotEntity snapshot,
        CaseEditableData data,
        ActionActor actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(data);
        void Text(string name, string? value) =>
            SetConfirmed(context, snapshot, name, CaseDataCodes.Text, value, actor, now);
        void Whole(string name, long? value) =>
            SetConfirmed(context, snapshot, name, CaseDataCodes.Integer, Integer(value), actor, now);
        void Day(string name, DateOnly? value) =>
            SetConfirmed(context, snapshot, name, CaseDataCodes.Date, Date(value), actor, now);
        // A mileage with no stated unit is in miles: the default, never
        // staff's choice (R1080). Staff's row holds a unit only where it
        // differs from the one standing without it (CaseOdometer.UnitWrite).
        void MileageUnit(string? unit)
        {
            const string name = CaseDataFieldNames.VehicleMileageUnit;
            var staff = snapshot.Fields.SingleOrDefault(
                item => item.FieldName == name && item.ValueKind == CaseDataCodes.Confirmed);
            if (unit is null)
            {
                // A mileage staff emptied empties its unit with it (R1077).
                // With no reading at all there is no unit to clear, so staff's
                // own unit goes and nothing is recorded over the instruction's.
                if (CaseDataFieldValues.IsClearedField(snapshot.Fields, CaseDataFieldNames.VehicleMileage))
                {
                    Text(name, null);
                }
                else if (staff is not null)
                {
                    context.CaseDataFields.Remove(staff);
                    snapshot.Fields.Remove(staff);
                }
                return;
            }

            // A cleared unit row reads as no stated unit: miles (R1080).
            var extracted = snapshot.Fields.SingleOrDefault(
                item => item.FieldName == name && item.ValueKind == CaseDataCodes.Fact);
            var write = CaseOdometer.UnitWrite((staff ?? extracted)?.Value, extracted?.Value, unit);
            if (write == CaseOdometerUnitWrite.RecordStaffValue)
            {
                Text(name, unit);
            }
            else if (write == CaseOdometerUnitWrite.RemoveStaffValue && staff is not null)
            {
                context.CaseDataFields.Remove(staff);
                snapshot.Fields.Remove(staff);
            }
        }

        Text(CaseDataFieldNames.ClaimantName, data.ClaimantName);
        Text(CaseDataFieldNames.ClaimantContactNumber, data.ClaimantContactNumber);
        Text(CaseDataFieldNames.ClaimantAddress, data.ClaimantAddress);
        Text(CaseDataFieldNames.ClaimNumber, data.ClaimNumber);
        Text(CaseDataFieldNames.VehicleRegistration, data.VehicleRegistration);
        Text(CaseDataFieldNames.VehicleMake, data.VehicleMake);
        Text(CaseDataFieldNames.VehicleModel, data.VehicleModel);
        Text(CaseDataFieldNames.VehicleYear, data.VehicleYear);
        Whole(CaseDataFieldNames.VehicleMileage, data.VehicleMileage);
        MileageUnit(data.VehicleMileageUnit);
        Text(CaseDataFieldNames.AccidentCircumstances, data.AccidentCircumstances);
        Day(CaseDataFieldNames.IncidentDate, data.IncidentDate);
        Text(CaseDataFieldNames.VatStatus, data.VatStatus);
        Day(CaseDataFieldNames.InspectionDate, data.InspectionDate);
        Day(CaseDataFieldNames.InspectionDeadline, data.InspectionDeadline);
        Text(CaseDataFieldNames.InspectionAddress, data.InspectionAddress);
        SetConfirmed(
            context,
            snapshot,
            CaseDataFieldNames.InspectionMode,
            CaseDataCodes.InspectionMode,
            InspectionMode(data.InspectionMode),
            actor,
            now);
        Text(CaseDataFieldNames.StorageLocation, data.StorageLocation);
        Text(CaseDataFieldNames.RepairerAddress, data.RepairerAddress);
        Text(CaseDataFieldNames.RepairerName, data.RepairerName);
        Text(CaseDataFieldNames.RepairerId, Identifier(data.RepairerId));
        Whole(CaseDataFieldNames.RepairerVersion, data.RepairerVersion);
        Text(CaseDataFieldNames.ClaimSourceId, Identifier(data.ClaimSourceId));
        Whole(CaseDataFieldNames.ClaimSourceVersion, data.ClaimSourceVersion);
        Text(CaseDataFieldNames.ClaimSourceName, data.ClaimSourceName);
        Text(CaseDataFieldNames.ClaimSourceContactName, data.ClaimSourceContactName);
        Text(CaseDataFieldNames.ClaimSourceContactTelephone, data.ClaimSourceContactTelephone);
        Text(CaseDataFieldNames.ClaimSourceContactEmailAddress, data.ClaimSourceContactEmailAddress);
        Text(CaseDataFieldNames.VehicleMileageDisplayUnit, data.VehicleMileageDisplayUnit);
        Text(CaseDataFieldNames.InspectionAddressTreatment, Named(data.InspectionAddressTreatment));
        Text(CaseDataFieldNames.InspectionLocationChoice, Named(data.InspectionLocationChoice));
        Text(CaseDataFieldNames.InspectionLocationSourceKind, Named(data.InspectionLocationSource));
        Text(CaseDataFieldNames.InspectionLocationSourceId, Identifier(data.InspectionLocationSourceId));
        Whole(CaseDataFieldNames.InspectionLocationSourceVersion, data.InspectionLocationSourceVersion);
        Text(CaseDataFieldNames.InspectionLocationSourceLabel, data.InspectionLocationSourceLabel);
        Text(CaseDataFieldNames.InspectionVehiclePresent, Flag(data.InspectionVehiclePresent));
        Text(CaseDataFieldNames.InspectionCondition, data.InspectionCondition);
        Text(CaseDataFieldNames.InspectionNotes, data.InspectionNotes);
        Text(CaseDataFieldNames.PrincipalNotes, data.PrincipalNotes);
        Text(CaseDataFieldNames.ClaimSourceNotes, data.ClaimSourceNotes);
        snapshot.ClaimSourceOverrideContactName = data.ClaimSourceOverrideContactName;
        snapshot.ClaimSourceOverrideContactTelephone = data.ClaimSourceOverrideContactTelephone;
        snapshot.ClaimSourceOverrideContactEmailAddress = data.ClaimSourceOverrideContactEmailAddress;
    }

    /// <summary>
    /// The work's registration as the vehicle lookup reads it
    /// (<see cref="EfVehicleWorkflowStore.CurrentRegistration"/>).
    /// </summary>
    public static string? Registration(CaseDataSnapshotEntity snapshot) =>
        EfVehicleWorkflowStore.CurrentRegistration(
            snapshot.Fields
                .Where(item => item.FieldName == CaseDataFieldNames.VehicleRegistration)
                .Select(item => (item.ValueKind, item.Value)));

    /// <summary>
    /// The facts only the vehicle lookup records
    /// (<see cref="Pegasus.Core.Assessment.AssessmentVocabulary.LookupDerivedPaths"/>)
    /// describe the vehicle it looked up. A save that changed the work's
    /// registration from <paramref name="registrationBefore"/> removes them in
    /// its own transaction, so the registration it now names never inherits
    /// the previous vehicle's facts; a lookup of that registration records them
    /// again. Returns the removed rows. A report that printed them is staled by
    /// the save's own freshness check, because the registration it printed
    /// changed too.
    /// </summary>
    public static async Task<IReadOnlyList<CaseAssessmentFieldEntity>> RemoveLookupFactsOnRegistrationChangeAsync(
        PegasusDbContext context,
        CaseDataSnapshotEntity snapshot,
        string? registrationBefore,
        CancellationToken cancellationToken)
    {
        if (string.Equals(Registration(snapshot), registrationBefore, StringComparison.Ordinal))
        {
            return [];
        }

        string[] paths = [.. Pegasus.Core.Assessment.AssessmentVocabulary.LookupDerivedPaths];
        var facts = await context.CaseAssessmentFields
            .Where(item => item.WorkId == snapshot.WorkId && paths.Contains(item.FieldPath))
            .ToListAsync(cancellationToken);
        context.CaseAssessmentFields.RemoveRange(facts);
        return facts;
    }

    /// <summary>
    /// The editable Case data as the Case shows it: each field's accepted
    /// value (<see cref="Accepted"/>).
    /// </summary>
    public static CaseEditableData ReadEditable(CaseDataSnapshotEntity snapshot) => new(
        AcceptedText(snapshot, CaseDataFieldNames.ClaimantName),
        AcceptedText(snapshot, CaseDataFieldNames.ClaimNumber),
        AcceptedText(snapshot, CaseDataFieldNames.VehicleRegistration),
        AcceptedText(snapshot, CaseDataFieldNames.VehicleMake),
        AcceptedText(snapshot, CaseDataFieldNames.VehicleModel),
        AcceptedLong(snapshot, CaseDataFieldNames.VehicleMileage),
        // A mileage with no stated unit is in miles (R1080), so an unrelated
        // save neither changes the unit nor names it in history.
        CaseOdometer.UnitOf(
            AcceptedLong(snapshot, CaseDataFieldNames.VehicleMileage),
            AcceptedText(snapshot, CaseDataFieldNames.VehicleMileageUnit)),
        AcceptedText(snapshot, CaseDataFieldNames.AccidentCircumstances),
        AcceptedDate(snapshot, CaseDataFieldNames.IncidentDate),
        AcceptedText(snapshot, CaseDataFieldNames.VatStatus),
        AcceptedDate(snapshot, CaseDataFieldNames.InspectionDate),
        AcceptedDate(snapshot, CaseDataFieldNames.InspectionDeadline),
        AcceptedText(snapshot, CaseDataFieldNames.InspectionAddress),
        AcceptedInspectionMode(snapshot, CaseDataFieldNames.InspectionMode),
        AcceptedText(snapshot, CaseDataFieldNames.ClaimantContactNumber),
        AcceptedText(snapshot, CaseDataFieldNames.ClaimantAddress),
        AcceptedText(snapshot, CaseDataFieldNames.StorageLocation),
        AcceptedText(snapshot, CaseDataFieldNames.RepairerAddress),
        AcceptedGuid(snapshot, CaseDataFieldNames.ClaimSourceId),
        AcceptedLong(snapshot, CaseDataFieldNames.ClaimSourceVersion),
        AcceptedText(snapshot, CaseDataFieldNames.ClaimSourceName),
        AcceptedText(snapshot, CaseDataFieldNames.ClaimSourceContactName),
        AcceptedText(snapshot, CaseDataFieldNames.ClaimSourceContactTelephone),
        AcceptedText(snapshot, CaseDataFieldNames.ClaimSourceContactEmailAddress),
        AcceptedText(snapshot, CaseDataFieldNames.VehicleMileageDisplayUnit),
        AcceptedEnum<CaseReportAddressTreatment>(snapshot, CaseDataFieldNames.InspectionAddressTreatment),
        AcceptedEnum<InspectionAddressChoiceKind>(snapshot, CaseDataFieldNames.InspectionLocationChoice),
        AcceptedEnum<InspectionLocationSourceKind>(snapshot, CaseDataFieldNames.InspectionLocationSourceKind),
        AcceptedGuid(snapshot, CaseDataFieldNames.InspectionLocationSourceId),
        AcceptedLong(snapshot, CaseDataFieldNames.InspectionLocationSourceVersion),
        AcceptedText(snapshot, CaseDataFieldNames.InspectionLocationSourceLabel),
        AcceptedFlag(snapshot, CaseDataFieldNames.InspectionVehiclePresent),
        AcceptedText(snapshot, CaseDataFieldNames.InspectionCondition),
        AcceptedText(snapshot, CaseDataFieldNames.InspectionNotes),
        AcceptedText(snapshot, CaseDataFieldNames.RepairerName),
        AcceptedGuid(snapshot, CaseDataFieldNames.RepairerId),
        AcceptedLong(snapshot, CaseDataFieldNames.RepairerVersion),
        AcceptedText(snapshot, CaseDataFieldNames.VehicleYear),
        AcceptedText(snapshot, CaseDataFieldNames.PrincipalNotes),
        AcceptedText(snapshot, CaseDataFieldNames.ClaimSourceNotes),
        DueBy: null,
        ClaimSourceOverrideContactName: snapshot.ClaimSourceOverrideContactName,
        ClaimSourceOverrideContactTelephone: snapshot.ClaimSourceOverrideContactTelephone,
        ClaimSourceOverrideContactEmailAddress: snapshot.ClaimSourceOverrideContactEmailAddress);

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
            Clear(context, snapshot, fieldName, valueType, existing, actor, now);
            return;
        }

        // A section save also carries unedited accepted values. Keeping one
        // must not manufacture a confirmation or rewrite its attribution.
        var accepted = CaseDataFieldValues.AcceptedField(snapshot.Fields, fieldName);
        if (accepted is not null && accepted.ValueType == valueType
            && string.Equals(accepted.Value, value, StringComparison.Ordinal))
        {
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
                WorkId = snapshot.WorkId,
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

    /// <summary>
    /// Staff emptied the box (R1077). With an extracted, looked-up or
    /// suggested value behind it, staff's row records the clear: an empty
    /// value under their name and time, so the value behind it is no longer
    /// shown or used, and it stays in history. A box already cleared is left
    /// as it was. With nothing behind it, staff's own value is removed. A
    /// suggestion alone is never shown in the box, so a box that stood on
    /// nothing else was already empty and records nothing.
    /// </summary>
    private static void Clear(
        PegasusDbContext context,
        CaseDataSnapshotEntity snapshot,
        string fieldName,
        string valueType,
        CaseDataFieldEntity? existing,
        ActionActor actor,
        DateTimeOffset now)
    {
        if (existing is not null && CaseDataFieldValues.IsCleared(existing))
        {
            return;
        }

        var standsBehind = snapshot.Fields.Any(
            item => item.FieldName == fieldName
                && (existing is null
                    ? item.ValueKind == CaseDataCodes.Fact
                    : item.ValueKind is CaseDataCodes.Fact or CaseDataCodes.Suggestion));
        if (!standsBehind)
        {
            if (existing is not null)
            {
                context.CaseDataFields.Remove(existing);
                snapshot.Fields.Remove(existing);
            }
            return;
        }

        if (existing is null)
        {
            existing = new()
            {
                WorkId = snapshot.WorkId,
                Snapshot = snapshot,
                FieldName = fieldName,
                ValueKind = CaseDataCodes.Confirmed,
                ValueType = valueType,
                Value = CaseDataFieldValues.ClearedValue,
                SourceKind = CaseDataCodes.StaffCorrection,
                SourceIdentity = actor.SubjectId,
                SourceLabel = "staff case-data correction",
                PolicyKey = CaseDataPolicy.EditPolicyKey,
                PolicyVersion = CaseDataPolicy.EditPolicyVersion,
                ConfirmedByActor = actor.SubjectId,
                ConfirmedAtUtc = now
            };
            snapshot.Fields.Add(existing);
            return;
        }

        existing.ValueType = valueType;
        existing.Value = CaseDataFieldValues.ClearedValue;
        existing.SourceKind = CaseDataCodes.StaffCorrection;
        existing.SourceIdentity = actor.SubjectId;
        existing.SourceLabel = "staff case-data correction";
        existing.PolicyKey = CaseDataPolicy.EditPolicyKey;
        existing.PolicyVersion = CaseDataPolicy.EditPolicyVersion;
        existing.ConfirmedByActor = actor.SubjectId;
        existing.ConfirmedAtUtc = now;
    }

    private static string? AcceptedText(CaseDataSnapshotEntity snapshot, string name) =>
        Accepted(snapshot, name)?.Value;

    private static long? AcceptedLong(CaseDataSnapshotEntity snapshot, string name) =>
        Accepted(snapshot, name) is { } field
            ? long.Parse(field.Value, NumberStyles.None, CultureInfo.InvariantCulture)
            : null;

    private static DateOnly? AcceptedDate(CaseDataSnapshotEntity snapshot, string name) =>
        Accepted(snapshot, name) is { } field
            ? DateOnly.ParseExact(field.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

    private static Guid? AcceptedGuid(CaseDataSnapshotEntity snapshot, string name) =>
        Accepted(snapshot, name) is { } field
            ? Guid.ParseExact(field.Value, "D")
            : null;

    private static bool? AcceptedFlag(CaseDataSnapshotEntity snapshot, string name) =>
        Accepted(snapshot, name) is { } field
            ? field.Value switch
            {
                "true" => true,
                "false" => false,
                _ => throw new InvalidDataException(
                    $"Unknown persisted case-data flag '{field.Value}' for '{name}'.")
            }
            : null;

    private static T? AcceptedEnum<T>(CaseDataSnapshotEntity snapshot, string name)
        where T : struct, Enum =>
        Accepted(snapshot, name) is { } field
            ? Enum.TryParse<T>(field.Value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new InvalidDataException(
                    $"Unknown persisted case-data value '{field.Value}' for '{name}'.")
            : null;

    private static CaseInspectionMode? AcceptedInspectionMode(
        CaseDataSnapshotEntity snapshot,
        string name) =>
        Accepted(snapshot, name) is { } field
            ? ParseInspectionMode(field.Value)
            : null;

    /// <summary>
    /// The accepted row (<see cref="CaseDataFieldValues.AcceptedField"/>):
    /// reading the same rule on both sides of a save means an untouched fact
    /// reads equal and is neither rewritten nor named as changed (#837).
    /// </summary>
    private static CaseDataFieldEntity? Accepted(CaseDataSnapshotEntity snapshot, string name) =>
        CaseDataFieldValues.AcceptedField(snapshot.Fields, name);

    private static string? Integer(long? value) =>
        value?.ToString(CultureInfo.InvariantCulture);

    private static string? Date(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? Identifier(Guid? value) => value?.ToString("D");

    private static string? Flag(bool? value) => value switch
    {
        null => null,
        true => "true",
        false => "false"
    };

    private static string? Named<T>(T? value)
        where T : struct, Enum => value?.ToString();

    public static string? InspectionMode(CaseInspectionMode? value) => value switch
    {
        null => null,
        CaseInspectionMode.PhysicalAddress => "physical_address",
        CaseInspectionMode.ImageBasedAssessment => "image_based_assessment",
        _ => throw new InvalidDataException("The inspection mode is invalid.")
    };

    public static CaseInspectionMode ParseInspectionMode(string value) => value switch
    {
        "physical_address" => CaseInspectionMode.PhysicalAddress,
        "image_based_assessment" => CaseInspectionMode.ImageBasedAssessment,
        _ => throw new InvalidDataException(
            $"Unknown persisted inspection mode '{value}'.")
    };
}

/// <summary>
/// The one owner of the chase schedule a case save leaves behind. Both the
/// case-detail save and the Case workspace save reschedule the same way, so an
/// edited deadline cannot mean two different chase dates.
/// </summary>
internal static class CaseDueWorkScheduler
{
    private const string MissingMaterialReason = "Case completeness is not confirmed";

    public static async Task ScheduleAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        DateOnly? dueBy,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workflow);
        if (workflow.DueWork is not { } due)
        {
            due = new()
            {
                CaseId = workflow.CaseId,
                Workflow = workflow,
                MissingMaterialReason = MissingMaterialReason,
                DueBy = dueBy,
                State = nameof(CaseDueWorkState.Scheduled),
                NextChaseAtUtc = CaseChaseSchedule.NextChaseAt(now, (await EfWorkflowConfigurationStore.ReadAsync(context, cancellationToken)).ChaseIntervalDays),
                Version = 0
            };
            workflow.DueWork = due;
            context.CaseDueWork.Add(due);
            return;
        }

        due.MissingMaterialReason = MissingMaterialReason;
        if (!due.DueBySetByStaff)
        {
            due.DueBy = dueBy;
        }
        if (due.State == nameof(CaseDueWorkState.Held)
            || (due.State == nameof(CaseDueWorkState.Scheduled) && due.NextChaseAtUtc is not null))
        {
            due.Version++;
            return;
        }
        due.State = nameof(CaseDueWorkState.Scheduled);
        due.NextChaseAtUtc = CaseChaseSchedule.NextChaseAt(now, (await EfWorkflowConfigurationStore.ReadAsync(context, cancellationToken)).ChaseIntervalDays);
        due.HeldAtUtc = null;
        due.RemainingChaseIntervalTicks = null;
        due.Version++;
    }

    public static void ApplyStaffDueBy(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        DateOnly? manualDueBy,
        DateOnly? acceptedDeadline,
        long? versionBeforeSave)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workflow);
        var resolved = CaseDuePolicy.Resolve(manualDueBy, acceptedDeadline);
        if (workflow.DueWork is not { } due)
        {
            due = new()
            {
                CaseId = workflow.CaseId,
                Workflow = workflow,
                MissingMaterialReason = MissingMaterialReason,
                DueBy = resolved,
                DueBySetByStaff = manualDueBy is not null,
                State = nameof(CaseDueWorkState.Stopped),
                NextChaseAtUtc = null,
                Version = 0
            };
            workflow.DueWork = due;
            context.CaseDueWork.Add(due);
            return;
        }

        due.DueBy = resolved;
        due.DueBySetByStaff = manualDueBy is not null;
        if (versionBeforeSave == due.Version)
        {
            due.Version++;
        }
    }

    public static void ProjectDueBy(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        DateOnly? acceptedDeadline,
        long? versionBeforeSave)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workflow);
        if (workflow.DueWork is not { } due)
        {
            due = new()
            {
                CaseId = workflow.CaseId,
                Workflow = workflow,
                MissingMaterialReason = MissingMaterialReason,
                DueBy = acceptedDeadline,
                State = nameof(CaseDueWorkState.Stopped),
                NextChaseAtUtc = null,
                Version = 0
            };
            workflow.DueWork = due;
            context.CaseDueWork.Add(due);
            return;
        }

        if (due.DueBySetByStaff || due.DueBy == acceptedDeadline)
        {
            return;
        }

        due.DueBy = acceptedDeadline;
        if (versionBeforeSave == due.Version)
        {
            due.Version++;
        }
    }
}
