using System.Globalization;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Cases;

/// <summary>
/// How the report describes where the vehicle was when it was assessed. Every
/// Collision Engineers assessment is desktop, so this is never a claim that an
/// Engineer attended: the report location is undetermined, the accepted Image
/// Based Assessment instruction, or a selected physical location the vehicle
/// is at.
/// </summary>
public enum CaseReportAddressTreatment
{
    Undetermined,
    ImageBasedAssessment,
    PhysicalVehicleLocation
}

public enum CaseOdometerUnit
{
    Miles,
    Kilometres
}

/// <summary>
/// The one owner of odometer units and their conversion. The recorded original
/// value and unit are never rewritten: a display in the other unit is computed
/// from the original every time, so repeatedly switching the display can never
/// re-convert an already rounded number. Zero is a reading, not an absence.
/// </summary>
public static class CaseOdometer
{
    public const decimal KilometresPerMile = 1.609344m;

    public static string Format(CaseOdometerUnit unit) => unit switch
    {
        CaseOdometerUnit.Miles => "miles",
        CaseOdometerUnit.Kilometres => "kilometres",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), "The odometer unit is invalid.")
    };

    public static bool TryParseUnit(string? value, out CaseOdometerUnit unit)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "mile":
            case "miles":
            case "mi":
                unit = CaseOdometerUnit.Miles;
                return true;
            case "kilometre":
            case "kilometres":
            case "kilometer":
            case "kilometers":
            case "km":
                unit = CaseOdometerUnit.Kilometres;
                return true;
            default:
                unit = default;
                return false;
        }
    }

    /// <summary>
    /// The recorded reading expressed in <paramref name="displayUnit"/>. The
    /// result is derived from the original every time, so a caller that
    /// toggles the display never feeds a rounded display back in.
    /// </summary>
    public static decimal Display(
        long originalValue,
        CaseOdometerUnit originalUnit,
        CaseOdometerUnit displayUnit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(originalValue);
        if (originalUnit == displayUnit)
        {
            return originalValue;
        }

        return originalUnit == CaseOdometerUnit.Miles
            ? originalValue * KilometresPerMile
            : originalValue / KilometresPerMile;
    }
}

/// <summary>
/// Where a selected inspection location came from. The choice is the operator's
/// pick from the directory suggestions; the source identity and version are the
/// exact maintained row that was copied, so a later change to that row never
/// silently rewrites the case.
/// </summary>
public sealed record CaseLocationProvenance(
    InspectionAddressChoiceKind? SelectedChoice,
    InspectionLocationSourceKind? SourceKind,
    Guid? DirectorySourceId,
    long? DirectorySourceVersion,
    string? DirectorySourceLabel);

public sealed record CaseWorkspaceStorageBusiness(
    Guid? DirectoryOrganizationId,
    long? DirectoryOrganizationVersion,
    string? Name,
    string? ContactName,
    string? ContactTelephone,
    string? ContactEmailAddress);

/// <summary>
/// The claim source recorded on this case: a copied snapshot of the selected
/// maintained record plus the note that belongs to this case alone. The claim
/// source is distinct from the principal, the sender, the insurer and any
/// third-party engineer.
/// </summary>
public sealed record CaseWorkspaceClaimSource(
    Guid? ClaimSourceId,
    long? ClaimSourceVersion,
    string? Name,
    string? ContactName,
    string? ContactTelephone,
    string? ContactEmailAddress,
    string? CaseNote);

public sealed record CaseWorkspaceOdometer(
    long? OriginalValue,
    CaseOdometerUnit? OriginalUnit,
    string? Source,
    CaseOdometerUnit? DisplayUnit);

public sealed record CaseWorkspaceOverview(
    string? ClaimantName,
    string? ClaimantContactNumber,
    string? ClaimantAddress,
    string? ClaimNumber,
    string? ContactName,
    string? ContactEmailAddress,
    string? ContactPhoneNumber,
    DateOnly? IncidentDate,
    string? AccidentCircumstances,
    DateOnly? InstructionDate,
    string? VatStatus,
    string? RepairerAddress,
    CaseWorkspaceClaimSource? ClaimSource);

public sealed record CaseWorkspaceInspection(
    CaseReportAddressTreatment? AddressTreatment,
    string? Address,
    CaseLocationProvenance? Provenance,
    string? StorageLocation,
    CaseWorkspaceStorageBusiness? StorageBusiness,
    DateOnly? InspectionDate,
    DateOnly? InspectionDeadline,
    bool? VehiclePresent,
    string? VehicleConditionAtInspection,
    string? ContactName,
    string? ContactTelephone,
    string? ContactEmailAddress,
    string? Notes,
    decimal? StoragePerDay,
    decimal? RecoveryCharge);

public sealed record CaseWorkspaceVehicle(
    string? Registration,
    string? Make,
    string? Model,
    CaseWorkspaceOdometer? Odometer,
    IReadOnlyDictionary<string, string?>? AssessmentFields);

public sealed record CaseWorkspaceDamage(
    IReadOnlyList<AssessmentImpact>? Impacts,
    IReadOnlyDictionary<string, string?>? AssessmentFields);

/// <summary>
/// The valuation working inputs the Case save may retain. Adopting a value is
/// the separate Apply command's act, so a finding path here fails closed.
/// </summary>
public sealed record CaseWorkspaceValuationDraft(
    IReadOnlyDictionary<string, string?>? DraftInputs);

public sealed record CaseWorkspaceEstimate(
    Guid? EstimateId,
    EstimateDetails? Details,
    IReadOnlyList<EstimateLineInput>? Lines);

public sealed record CaseWorkspaceSettlement(
    IReadOnlyDictionary<string, string?>? AssessmentFields);

public sealed record CaseWorkspaceReport(
    IReadOnlyDictionary<string, string?>? AssessmentFields,
    Guid? SignOffEngineerId,
    DateOnly? ReportDate);

/// <summary>
/// The two factual completeness controls. There is no Confirm requirement and
/// no review flag: readiness is evaluated from these persisted facts by the one
/// Core policy inside the save's own transaction, never from a posted boolean.
/// </summary>
public sealed record CaseWorkspaceCompleteness(
    bool? InstructionComplete,
    bool? ImagesComplete);

/// <summary>
/// One Case edit. Every section is optional: a null section was not submitted
/// and is left exactly as persisted, while a submitted section replaces its
/// own members — a null member inside it clears that value. Engineer notes and
/// Case notes are separately attributed append commands and are deliberately
/// absent from this replace-style payload.
/// </summary>
public sealed record SaveCaseWorkspaceRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken)
{
    public CaseWorkspaceOverview? Overview { get; init; }

    public CaseWorkspaceInspection? Inspection { get; init; }

    public CaseWorkspaceVehicle? Vehicle { get; init; }

    public CaseWorkspaceDamage? Damage { get; init; }

    public CaseWorkspaceValuationDraft? Valuation { get; init; }

    public CaseWorkspaceEstimate? Estimate { get; init; }

    public CaseWorkspaceSettlement? Settlement { get; init; }

    public CaseWorkspaceReport? Report { get; init; }

    public CaseWorkspaceCompleteness? Completeness { get; init; }

    public bool IsEmpty =>
        Overview is null && Inspection is null && Vehicle is null && Damage is null
        && Valuation is null && Estimate is null && Settlement is null && Report is null
        && Completeness is null;
}

/// <summary>
/// The whole Case surface as it stands after the save. The version, the
/// completeness projection and the readiness rail are read off the projections
/// they already belong to rather than repeated here.
/// </summary>
public sealed record SaveCaseWorkspaceResult(
    CaseDataProjection Data,
    CaseAssessmentProjection Assessment,
    RepairSpecificationVersion? Estimate,
    bool WasReplay)
{
    public long Version => Data.Version;

    public CaseCompletenessProjection Completeness => Data.Completeness;

    public IReadOnlyList<AssessmentReadinessItem> Readiness => Assessment.Readiness;
}

public interface ICaseWorkspaceStore
{
    Task<SaveCaseWorkspaceResult> SaveAsync(
        SaveCaseWorkspaceRequest request,
        CancellationToken cancellationToken);
}

public interface ISaveCaseWorkspace
{
    Task<SaveCaseWorkspaceResult> ExecuteAsync(
        SaveCaseWorkspaceRequest request,
        CancellationToken cancellationToken);
}

public sealed class SaveCaseWorkspace(
    ICaseWorkspaceStore store,
    IStaffAccountQueries staffAccounts) : ISaveCaseWorkspace
{
    private readonly ICaseWorkspaceStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IStaffAccountQueries _staffAccounts =
        staffAccounts ?? throw new ArgumentNullException(nameof(staffAccounts));

    public async Task<SaveCaseWorkspaceResult> ExecuteAsync(
        SaveCaseWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = CaseWorkspacePolicy.ValidateAndNormalize(request);
        if (normalized.Report?.SignOffEngineerId is { } signOffEngineerId)
        {
            CaseSignOffEngineerResolver.RequireEligible(
                await _staffAccounts.ListSignOffEngineersAsync(cancellationToken),
                signOffEngineerId);
        }

        return await _store.SaveAsync(normalized, cancellationToken);
    }
}

/// <summary>
/// Validation for the one Case edit. It owns nothing itself: each section is
/// checked by the business owner that already holds that rule —
/// <see cref="CaseDataPolicy"/> for case facts, <see cref="AssessmentPolicy"/>
/// for assessment paths and damage impacts, <see cref="EstimatePolicy"/> and
/// <see cref="RepairSpecificationPolicy"/> for the draft estimate. What this
/// policy adds is the shape of the payload, the union of the assessment paths
/// it writes, and the refusals that only make sense across the whole payload.
/// </summary>
public static class CaseWorkspacePolicy
{
    public const string PolicyKey = "case-workspace-save";
    public const int PolicyVersion = 1;

    public static SaveCaseWorkspaceRequest ValidateAndNormalize(SaveCaseWorkspaceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CaseDataPolicy.ValidateMutation(request);
        if (request.Actor.Kind is not (ActorKind.Staff or ActorKind.Automation))
        {
            throw new InvalidOperationException(
                "Only a staff member or the Automation actor can save the Case workspace.");
        }

        if (request.IsEmpty)
        {
            throw new ArgumentException(
                "A Case save requires at least one submitted section.",
                nameof(request));
        }

        var fields = AssessmentFields(request);
        if (request.Actor.Kind == ActorKind.Staff
            && fields.Keys.Any(path => AssessmentVocabulary.Definitions[path].IsFinding))
        {
            AssessmentPolicy.RequireFindingConfirmationAuthority(request.Actor);
        }

        if (request.Inspection is { } inspection)
        {
            _ = CaseDataPolicy.ResolveInspection(
                inspection.AddressTreatment ?? CaseReportAddressTreatment.Undetermined,
                inspection.Address);
        }

        if (request.Report?.SignOffEngineerId == Guid.Empty)
        {
            throw new ArgumentException(
                "A Sign-off Engineer identifier cannot be empty.",
                nameof(request));
        }

        return request.Estimate is { } estimate
            ? request with { Estimate = ValidateEstimate(estimate, request.Actor) }
            : request;
    }

    /// <summary>
    /// Every assessment path this save writes, normalized once. A path belongs
    /// to exactly one section, so the same path submitted twice fails closed
    /// instead of letting the section order decide.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> AssessmentFields(
        SaveCaseWorkspaceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal);

        void Add(string path, string? rawValue)
        {
            if (!fields.TryAdd(path, AssessmentPolicy.NormalizeWritableField(path, rawValue)))
            {
                throw new InvalidOperationException(
                    $"The field '{path}' was submitted by more than one section of the Case save.");
            }
        }

        void AddAll(IReadOnlyDictionary<string, string?>? section, string sectionName)
        {
            if (section is null)
            {
                return;
            }

            foreach (var (path, rawValue) in section)
            {
                if (TypedPaths.Contains(path))
                {
                    throw new InvalidOperationException(
                        $"The field '{path}' is written through the {sectionName} section's own "
                        + "member, not as a free assessment field.");
                }

                Add(path, rawValue);
            }
        }

        AddAll(request.Vehicle?.AssessmentFields, "Vehicle");
        AddAll(request.Damage?.AssessmentFields, "Damage");
        AddAll(request.Settlement?.AssessmentFields, "Settlement");
        AddAll(request.Report?.AssessmentFields, "Report");

        if (request.Valuation is { DraftInputs: { } draftInputs })
        {
            foreach (var (path, rawValue) in draftInputs)
            {
                if (!AssessmentVocabulary.Definitions.TryGetValue(path, out var definition)
                    || definition.IsFinding)
                {
                    throw new InvalidOperationException(
                        $"The valuation section retains working inputs only; '{path}' is a "
                        + "professional finding and is recorded by the valuation Apply command.");
                }

                Add(path, rawValue);
            }
        }

        if (request.Vehicle?.Odometer is { } odometer)
        {
            Add(AssessmentVocabulary.VehicleMileageSource, odometer.Source);
        }

        if (request.Damage is { } damage && damage.Impacts is not null)
        {
            Add(
                AssessmentVocabulary.DamageImpacts,
                AssessmentPolicy.SerializeImpacts(damage.Impacts));
        }

        if (request.Inspection is { } inspection)
        {
            Add(AssessmentVocabulary.SettlementStoragePerDay, Money(inspection.StoragePerDay));
            Add(AssessmentVocabulary.CostRecoveryCharge, Money(inspection.RecoveryCharge));
        }

        if (request.Report is { } report)
        {
            Add(
                AssessmentVocabulary.ReportDate,
                report.ReportDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (fields.Count > AssessmentPolicy.MaximumFieldsPerSave)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"A Case save is bounded to {AssessmentPolicy.MaximumFieldsPerSave} assessment fields.");
        }

        return fields;
    }

    /// <summary>
    /// The submitted sections laid over the persisted case facts. A section
    /// that was not submitted leaves its facts alone; a submitted section
    /// replaces every fact it owns, so a null member inside it clears the
    /// stored value.
    /// </summary>
    public static CaseEditableData Overlay(
        CaseEditableData persisted,
        SaveCaseWorkspaceRequest request)
    {
        ArgumentNullException.ThrowIfNull(persisted);
        ArgumentNullException.ThrowIfNull(request);
        var merged = persisted;
        if (request.Overview is { } overview)
        {
            merged = merged with
            {
                ClaimantName = overview.ClaimantName,
                ClaimantContactNumber = overview.ClaimantContactNumber,
                ClaimantAddress = overview.ClaimantAddress,
                ClaimNumber = overview.ClaimNumber,
                ContactName = overview.ContactName,
                ContactEmailAddress = overview.ContactEmailAddress,
                ContactPhoneNumber = overview.ContactPhoneNumber,
                IncidentDate = overview.IncidentDate,
                AccidentCircumstances = overview.AccidentCircumstances,
                InstructionDate = overview.InstructionDate,
                VatStatus = overview.VatStatus,
                RepairerAddress = overview.RepairerAddress,
                ClaimSourceId = overview.ClaimSource?.ClaimSourceId,
                ClaimSourceVersion = overview.ClaimSource?.ClaimSourceVersion,
                ClaimSourceName = overview.ClaimSource?.Name,
                ClaimSourceContactName = overview.ClaimSource?.ContactName,
                ClaimSourceContactTelephone = overview.ClaimSource?.ContactTelephone,
                ClaimSourceContactEmailAddress = overview.ClaimSource?.ContactEmailAddress,
                ClaimSourceCaseNote = overview.ClaimSource?.CaseNote
            };
        }

        if (request.Inspection is { } inspection)
        {
            var (address, mode) = CaseDataPolicy.ResolveInspection(
                inspection.AddressTreatment ?? CaseReportAddressTreatment.Undetermined,
                inspection.Address);
            merged = merged with
            {
                InspectionAddress = address,
                InspectionMode = mode,
                InspectionAddressTreatment = inspection.AddressTreatment,
                InspectionLocationChoice = inspection.Provenance?.SelectedChoice,
                InspectionLocationSource = inspection.Provenance?.SourceKind,
                InspectionLocationSourceId = inspection.Provenance?.DirectorySourceId,
                InspectionLocationSourceVersion = inspection.Provenance?.DirectorySourceVersion,
                InspectionLocationSourceLabel = inspection.Provenance?.DirectorySourceLabel,
                StorageLocation = inspection.StorageLocation,
                StorageBusinessId = inspection.StorageBusiness?.DirectoryOrganizationId,
                StorageBusinessVersion = inspection.StorageBusiness?.DirectoryOrganizationVersion,
                StorageBusinessName = inspection.StorageBusiness?.Name,
                StorageBusinessContactName = inspection.StorageBusiness?.ContactName,
                StorageBusinessContactTelephone = inspection.StorageBusiness?.ContactTelephone,
                StorageBusinessContactEmailAddress = inspection.StorageBusiness?.ContactEmailAddress,
                InspectionDate = inspection.InspectionDate,
                InspectionDeadline = inspection.InspectionDeadline,
                InspectionVehiclePresent = inspection.VehiclePresent,
                InspectionCondition = inspection.VehicleConditionAtInspection,
                InspectionContactName = inspection.ContactName,
                InspectionContactTelephone = inspection.ContactTelephone,
                InspectionContactEmailAddress = inspection.ContactEmailAddress,
                InspectionNotes = inspection.Notes
            };
        }

        if (request.Vehicle is { } vehicle)
        {
            merged = merged with
            {
                VehicleRegistration = vehicle.Registration,
                VehicleMake = vehicle.Make,
                VehicleModel = vehicle.Model,
                VehicleMileage = vehicle.Odometer?.OriginalValue,
                VehicleMileageUnit = vehicle.Odometer?.OriginalUnit is { } originalUnit
                    ? CaseOdometer.Format(originalUnit)
                    : null,
                VehicleMileageDisplayUnit = vehicle.Odometer?.DisplayUnit is { } displayUnit
                    ? CaseOdometer.Format(displayUnit)
                    : null
            };
        }

        return merged;
    }

    /// <summary>
    /// Paths a section owns through a typed member. Accepting them again as
    /// free-form assessment fields would give one fact two spellings in one
    /// payload.
    /// </summary>
    private static readonly HashSet<string> TypedPaths = new(
        StringComparer.Ordinal)
    {
        AssessmentVocabulary.DamageImpacts,
        AssessmentVocabulary.VehicleMileageSource,
        AssessmentVocabulary.SettlementStoragePerDay,
        AssessmentVocabulary.CostRecoveryCharge,
        AssessmentVocabulary.ReportDate
    };

    private static CaseWorkspaceEstimate ValidateEstimate(
        CaseWorkspaceEstimate estimate,
        ActionActor actor)
    {
        if (estimate.EstimateId == Guid.Empty)
        {
            throw new ArgumentException(
                "An estimate identifier cannot be empty when supplied.",
                nameof(estimate));
        }

        if (estimate.Details is null && estimate.Lines is null)
        {
            throw new ArgumentException(
                "A submitted estimate section requires its header, its lines, or both.",
                nameof(estimate));
        }

        RepairSpecificationPolicy.RequireEngineer(actor);
        return estimate with
        {
            Details = estimate.Details is null
                ? null
                : EstimatePolicy.ValidateDetails(estimate.Details),
            Lines = estimate.Lines is null
                ? null
                : AssessmentPolicy.NormalizeRepairSpecificationLines(estimate.Lines)
        };
    }

    private static string? Money(decimal? value) =>
        value?.ToString("0.00", CultureInfo.InvariantCulture);
}
