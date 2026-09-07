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
        var replay = await CaseOperationReplay.FindAsync(
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
        // PLAT-072: only the two factual controls are written. The
        // staff-confirmation columns are inert and keep whatever they hold.
        snapshot.Case.InstructionComplete = request.Completeness.InstructionComplete;
        snapshot.Case.ImagesComplete = request.Completeness.ImagesComplete;
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
            CaseDueWorkScheduler.Schedule(context, workflow, snapshot.Case.AcceptedInspectionDeadline, now);
        }

        var beforeVersion = workflow.Version;
        workflow.Version++;
        ClearLease(workflow);
        CaseMutationHistory.Add(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            request.Reason,
            "case_completeness_confirmed",
            requestHash,
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
        var replay = await CaseOperationReplay.FindAsync(
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

        var before = CaseDataFieldWriter.ReadEditable(snapshot);
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
        CaseDataFieldWriter.ApplyEditableData(context, snapshot, data, request.Actor, now);
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
        CaseDueWorkScheduler.Schedule(context, workflow, data.InspectionDeadline, now);

        var beforeVersion = workflow.Version;
        workflow.Version++;
        var completenessAfter = new CaseCompleteness(
            snapshot.Case.InstructionComplete,
            snapshot.Case.ImagesComplete,
            snapshot.Case.InstructionConfirmedByStaff,
            snapshot.Case.ImagesConfirmedByStaff);
        ClearLease(workflow);
        CaseMutationHistory.Add(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            request.Reason,
            "case_data_saved",
            requestHash,
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

    internal static IQueryable<CaseDataSnapshotEntity> SnapshotQuery(
        PegasusDbContext context,
        bool tracking)
    {
        var query = context.CaseDataSnapshots
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .Include(item => item.Fields);
        return tracking ? query : query.AsNoTracking();
    }

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


    internal static CaseDataProjection Map(
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
            EfIntakeReceiptStore.ParseSourceChannel(snapshot.OriginSourceChannel),
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
        new(
            TextField(snapshot, CaseDataFieldNames.ClaimantName),
            TextField(snapshot, CaseDataFieldNames.ClaimantContactNumber),
            TextField(snapshot, CaseDataFieldNames.ClaimantAddress)),
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
            InspectionModeField(snapshot, CaseDataFieldNames.InspectionMode),
            TextField(snapshot, CaseDataFieldNames.StorageLocation),
            TextField(snapshot, CaseDataFieldNames.RepairerAddress)),
        Workspace(snapshot));

    /// <summary>
    /// The v1 workspace facts. Each is entered by staff through the one Case
    /// save, so the current value is the whole story and the projection carries
    /// the value itself rather than a fact/suggestion/confirmed triple.
    /// </summary>
    private static CaseWorkspaceData Workspace(CaseDataSnapshotEntity snapshot)
    {
        var data = CaseDataFieldWriter.ReadEditable(snapshot);
        return new(
            new(
                data.ClaimSourceId,
                data.ClaimSourceVersion,
                data.ClaimSourceName,
                data.ClaimSourceContactName,
                data.ClaimSourceContactTelephone,
                data.ClaimSourceContactEmailAddress,
                data.ClaimSourceCaseNote),
            new(
                data.StorageBusinessId,
                data.StorageBusinessVersion,
                data.StorageBusinessName,
                data.StorageBusinessContactName,
                data.StorageBusinessContactTelephone,
                data.StorageBusinessContactEmailAddress),
            data.InspectionAddressTreatment,
            new(
                data.InspectionLocationChoice,
                data.InspectionLocationSource,
                data.InspectionLocationSourceId,
                data.InspectionLocationSourceVersion,
                data.InspectionLocationSourceLabel),
            data.InspectionVehiclePresent,
            data.InspectionCondition,
            data.InspectionContactName,
            data.InspectionContactTelephone,
            data.InspectionContactEmailAddress,
            data.InspectionNotes,
            data.VehicleMileageDisplayUnit is { } displayUnit
                && CaseOdometer.TryParseUnit(displayUnit, out var unit)
                    ? unit
                    : null);
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
        return CaseOperationReplay.Hash(material);
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
        CaseDataCodes.ProviderApi => CaseDataSourceKind.ProviderApi,
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

        Text(CaseDataFieldNames.ClaimantName, data.ClaimantName);
        Text(CaseDataFieldNames.ClaimantContactNumber, data.ClaimantContactNumber);
        Text(CaseDataFieldNames.ClaimantAddress, data.ClaimantAddress);
        Text(CaseDataFieldNames.ClaimNumber, data.ClaimNumber);
        Text(CaseDataFieldNames.VehicleRegistration, data.VehicleRegistration);
        Text(CaseDataFieldNames.VehicleMake, data.VehicleMake);
        Text(CaseDataFieldNames.VehicleModel, data.VehicleModel);
        Whole(CaseDataFieldNames.VehicleMileage, data.VehicleMileage);
        Text(CaseDataFieldNames.VehicleMileageUnit, data.VehicleMileageUnit);
        Text(CaseDataFieldNames.AccidentCircumstances, data.AccidentCircumstances);
        Day(CaseDataFieldNames.IncidentDate, data.IncidentDate);
        Text(CaseDataFieldNames.ContactName, data.ContactName);
        Text(CaseDataFieldNames.ContactEmailAddress, data.ContactEmailAddress);
        Text(CaseDataFieldNames.ContactPhoneNumber, data.ContactPhoneNumber);
        Day(CaseDataFieldNames.InstructionDate, data.InstructionDate);
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
        Text(CaseDataFieldNames.ClaimSourceId, Identifier(data.ClaimSourceId));
        Whole(CaseDataFieldNames.ClaimSourceVersion, data.ClaimSourceVersion);
        Text(CaseDataFieldNames.ClaimSourceName, data.ClaimSourceName);
        Text(CaseDataFieldNames.ClaimSourceContactName, data.ClaimSourceContactName);
        Text(CaseDataFieldNames.ClaimSourceContactTelephone, data.ClaimSourceContactTelephone);
        Text(CaseDataFieldNames.ClaimSourceContactEmailAddress, data.ClaimSourceContactEmailAddress);
        Text(CaseDataFieldNames.ClaimSourceCaseNote, data.ClaimSourceCaseNote);
        Text(CaseDataFieldNames.StorageBusinessId, Identifier(data.StorageBusinessId));
        Whole(CaseDataFieldNames.StorageBusinessVersion, data.StorageBusinessVersion);
        Text(CaseDataFieldNames.StorageBusinessName, data.StorageBusinessName);
        Text(CaseDataFieldNames.StorageBusinessContactName, data.StorageBusinessContactName);
        Text(CaseDataFieldNames.StorageBusinessContactTelephone, data.StorageBusinessContactTelephone);
        Text(
            CaseDataFieldNames.StorageBusinessContactEmailAddress,
            data.StorageBusinessContactEmailAddress);
        Text(CaseDataFieldNames.VehicleMileageDisplayUnit, data.VehicleMileageDisplayUnit);
        Text(CaseDataFieldNames.InspectionAddressTreatment, Named(data.InspectionAddressTreatment));
        Text(CaseDataFieldNames.InspectionLocationChoice, Named(data.InspectionLocationChoice));
        Text(CaseDataFieldNames.InspectionLocationSourceKind, Named(data.InspectionLocationSource));
        Text(CaseDataFieldNames.InspectionLocationSourceId, Identifier(data.InspectionLocationSourceId));
        Whole(CaseDataFieldNames.InspectionLocationSourceVersion, data.InspectionLocationSourceVersion);
        Text(CaseDataFieldNames.InspectionLocationSourceLabel, data.InspectionLocationSourceLabel);
        Text(CaseDataFieldNames.InspectionVehiclePresent, Flag(data.InspectionVehiclePresent));
        Text(CaseDataFieldNames.InspectionCondition, data.InspectionCondition);
        Text(CaseDataFieldNames.InspectionContactName, data.InspectionContactName);
        Text(CaseDataFieldNames.InspectionContactTelephone, data.InspectionContactTelephone);
        Text(CaseDataFieldNames.InspectionContactEmailAddress, data.InspectionContactEmailAddress);
        Text(CaseDataFieldNames.InspectionNotes, data.InspectionNotes);
    }

    public static CaseEditableData ReadEditable(CaseDataSnapshotEntity snapshot) => new(
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
        ConfirmedInspectionMode(snapshot, CaseDataFieldNames.InspectionMode),
        ConfirmedText(snapshot, CaseDataFieldNames.ClaimantContactNumber),
        ConfirmedText(snapshot, CaseDataFieldNames.ClaimantAddress),
        ConfirmedText(snapshot, CaseDataFieldNames.StorageLocation),
        ConfirmedText(snapshot, CaseDataFieldNames.RepairerAddress),
        ConfirmedGuid(snapshot, CaseDataFieldNames.ClaimSourceId),
        ConfirmedLong(snapshot, CaseDataFieldNames.ClaimSourceVersion),
        ConfirmedText(snapshot, CaseDataFieldNames.ClaimSourceName),
        ConfirmedText(snapshot, CaseDataFieldNames.ClaimSourceContactName),
        ConfirmedText(snapshot, CaseDataFieldNames.ClaimSourceContactTelephone),
        ConfirmedText(snapshot, CaseDataFieldNames.ClaimSourceContactEmailAddress),
        ConfirmedText(snapshot, CaseDataFieldNames.ClaimSourceCaseNote),
        ConfirmedGuid(snapshot, CaseDataFieldNames.StorageBusinessId),
        ConfirmedLong(snapshot, CaseDataFieldNames.StorageBusinessVersion),
        ConfirmedText(snapshot, CaseDataFieldNames.StorageBusinessName),
        ConfirmedText(snapshot, CaseDataFieldNames.StorageBusinessContactName),
        ConfirmedText(snapshot, CaseDataFieldNames.StorageBusinessContactTelephone),
        ConfirmedText(snapshot, CaseDataFieldNames.StorageBusinessContactEmailAddress),
        ConfirmedText(snapshot, CaseDataFieldNames.VehicleMileageDisplayUnit),
        ConfirmedEnum<CaseReportAddressTreatment>(snapshot, CaseDataFieldNames.InspectionAddressTreatment),
        ConfirmedEnum<InspectionAddressChoiceKind>(snapshot, CaseDataFieldNames.InspectionLocationChoice),
        ConfirmedEnum<InspectionLocationSourceKind>(snapshot, CaseDataFieldNames.InspectionLocationSourceKind),
        ConfirmedGuid(snapshot, CaseDataFieldNames.InspectionLocationSourceId),
        ConfirmedLong(snapshot, CaseDataFieldNames.InspectionLocationSourceVersion),
        ConfirmedText(snapshot, CaseDataFieldNames.InspectionLocationSourceLabel),
        ConfirmedFlag(snapshot, CaseDataFieldNames.InspectionVehiclePresent),
        ConfirmedText(snapshot, CaseDataFieldNames.InspectionCondition),
        ConfirmedText(snapshot, CaseDataFieldNames.InspectionContactName),
        ConfirmedText(snapshot, CaseDataFieldNames.InspectionContactTelephone),
        ConfirmedText(snapshot, CaseDataFieldNames.InspectionContactEmailAddress),
        ConfirmedText(snapshot, CaseDataFieldNames.InspectionNotes));

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

    private static Guid? ConfirmedGuid(CaseDataSnapshotEntity snapshot, string name) =>
        Confirmed(snapshot, name) is { } field
            ? Guid.ParseExact(field.Value, "D")
            : null;

    private static bool? ConfirmedFlag(CaseDataSnapshotEntity snapshot, string name) =>
        Confirmed(snapshot, name) is { } field
            ? field.Value switch
            {
                "true" => true,
                "false" => false,
                _ => throw new InvalidDataException(
                    $"Unknown persisted case-data flag '{field.Value}' for '{name}'.")
            }
            : null;

    private static T? ConfirmedEnum<T>(CaseDataSnapshotEntity snapshot, string name)
        where T : struct, Enum =>
        Confirmed(snapshot, name) is { } field
            ? Enum.TryParse<T>(field.Value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new InvalidDataException(
                    $"Unknown persisted case-data value '{field.Value}' for '{name}'.")
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

    public static void Schedule(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        DateOnly? dueBy,
        DateTimeOffset now)
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
                NextChaseAtUtc = CaseChaseSchedule.FirstChaseAt(now),
                Version = 0
            };
            workflow.DueWork = due;
            context.CaseDueWork.Add(due);
            return;
        }

        due.MissingMaterialReason = MissingMaterialReason;
        due.DueBy = dueBy;
        due.State = nameof(CaseDueWorkState.Scheduled);
        due.NextChaseAtUtc = CaseChaseSchedule.FirstChaseAt(now);
        due.HeldAtUtc = null;
        due.RemainingChaseIntervalTicks = null;
        due.Version++;
    }
}
