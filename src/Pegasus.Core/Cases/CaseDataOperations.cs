using Pegasus.Core.Address;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Cases;

public sealed class ConfirmCompleteness(
    ICaseDataStore store,
    ICaseWorkflowConfiguration configuration) : IConfirmCompleteness
{
    private readonly ICaseDataStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICaseWorkflowConfiguration _configuration =
        configuration ?? throw new ArgumentNullException(nameof(configuration));

    public async Task<CaseDataProjection> ExecuteAsync(
        ConfirmCompletenessRequest request,
        CancellationToken cancellationToken)
    {
        CaseDataPolicy.ValidateMutation(request);
        ArgumentNullException.ThrowIfNull(request.Completeness);
        CaseDataPolicy.ValidateCompleteness(request.Completeness);

        var currentConfiguration = await _configuration.GetCurrentAsync(cancellationToken);
        var evaluation = CaseCompletenessPolicy.Evaluate(
            request.Completeness,
            currentConfiguration);
        return await _store.ConfirmCompletenessAsync(
            request,
            evaluation,
            cancellationToken);
    }
}

public sealed class SaveCase(ICaseDataStore store) : ISaveCase
{
    private readonly ICaseDataStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<CaseDataProjection> ExecuteAsync(
        SaveCaseRequest request,
        CancellationToken cancellationToken)
    {
        CaseDataPolicy.ValidateMutation(request);
        var normalized = CaseDataPolicy.Normalize(request.Data);
        return _store.SaveAsync(request with { Data = normalized }, cancellationToken);
    }
}

public static class CaseCompletenessPolicy
{
    public static CaseCompletenessEvaluation Evaluate(
        CaseCompleteness completeness,
        CaseWorkflowConfiguration configuration,
        bool automaticallyDefinitive = false)
    {
        CaseDataPolicy.ValidateCompleteness(completeness);
        return EvaluateAcceptanceCommand(completeness, configuration, automaticallyDefinitive);
    }

    internal static CaseCompletenessEvaluation EvaluateAcceptanceCommand(
        CaseCompleteness completeness,
        CaseWorkflowConfiguration configuration,
        bool automaticallyDefinitive = false)
    {
        ArgumentNullException.ThrowIfNull(completeness);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.PolicyKey);
        if (configuration.PolicyKey.Length > 100 || configuration.PolicyVersion < 1)
        {
            throw new InvalidOperationException(
                "The current case-workflow policy identity is invalid.");
        }

        var satisfiesPolicy = completeness.IsReadyForReview(automaticallyDefinitive);

        return new(
            satisfiesPolicy,
            configuration.PolicyKey,
            configuration.PolicyVersion);
    }
}

public static class CaseDataPolicy
{
    public const string EditPolicyKey = "case-data-edit";
    public const int EditPolicyVersion = 1;

    public static void ValidateMutation(CaseMutationRequest request) =>
        CaseLifecycleRules.ValidateMutation(request);

    /// <summary>
    /// PLAT-072: completeness is two factual controls. The staff-confirmation
    /// flags are no longer written by any command, so there is no pairing left
    /// to police here; the columns survive only until their owner removes them.
    /// </summary>
    public static void ValidateCompleteness(CaseCompleteness completeness) =>
        ArgumentNullException.ThrowIfNull(completeness);

    /// <summary>
    /// The one place the stated report-address treatment becomes the stored
    /// address and inspection mode. Every CE assessment is desktop, so the
    /// treatment describes where the vehicle is for the report, never
    /// attendance: it is blank, the accepted Image Based Assessment
    /// instruction, or a selected physical vehicle location. The treatment is
    /// stated by the operator, never inferred from the text of an address.
    /// </summary>
    public static (string? Address, CaseInspectionMode? Mode) ResolveInspection(
        CaseReportAddressTreatment treatment,
        string? address)
    {
        if (!Enum.IsDefined(treatment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(treatment),
                "The report-address treatment is invalid.");
        }

        var trimmed = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        switch (treatment)
        {
            case CaseReportAddressTreatment.Undetermined:
                if (trimmed is not null)
                {
                    throw new InvalidOperationException(
                        "An undetermined report location cannot carry an address.");
                }

                return (null, null);

            case CaseReportAddressTreatment.ImageBasedAssessment:
                if (trimmed is not null
                    && !string.Equals(
                        trimmed,
                        Ext18InspectionAddressPolicy.ImageBasedAssessment,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Image Based Assessment requires the exact accepted instruction value.");
                }

                return (
                    Ext18InspectionAddressPolicy.ImageBasedAssessment,
                    CaseInspectionMode.ImageBasedAssessment);

            default:
                if (trimmed is null)
                {
                    throw new InvalidOperationException(
                        "A physical vehicle location requires an address.");
                }

                if (string.Equals(
                        trimmed,
                        Ext18InspectionAddressPolicy.ImageBasedAssessment,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The Image Based Assessment value cannot be saved as a physical address.");
                }

                return (trimmed, CaseInspectionMode.PhysicalAddress);
        }
    }

    public static CaseInspectionMode? InferInspectionMode(string? address) =>
        string.IsNullOrWhiteSpace(address)
            ? null
            : string.Equals(
                address.Trim(),
                Ext18InspectionAddressPolicy.ImageBasedAssessment,
                StringComparison.Ordinal)
                ? CaseInspectionMode.ImageBasedAssessment
                : CaseInspectionMode.PhysicalAddress;

    public static CaseEditableData Normalize(CaseEditableData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.VehicleMileage < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                "Vehicle mileage cannot be negative.");
        }

        if (data.InspectionMode is { } mode && !Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                "The inspection mode is invalid.");
        }

        ValidateDate(data.IncidentDate, nameof(data.IncidentDate));
        ValidateDate(data.InstructionDate, nameof(data.InstructionDate));
        ValidateDate(data.InspectionDate, nameof(data.InspectionDate));
        ValidateDate(data.InspectionDeadline, nameof(data.InspectionDeadline));

        var normalized = data with
        {
            ClaimantName = Text(data.ClaimantName, 300, nameof(data.ClaimantName)),
            ClaimantContactNumber = Text(data.ClaimantContactNumber, 100, nameof(data.ClaimantContactNumber)),
            ClaimantAddress = Paragraphs(data.ClaimantAddress, 1000, nameof(data.ClaimantAddress)),
            ClaimNumber = Text(data.ClaimNumber, 100, nameof(data.ClaimNumber)),
            VehicleRegistration = Registration(data.VehicleRegistration),
            VehicleMake = Text(data.VehicleMake, 100, nameof(data.VehicleMake)),
            VehicleModel = Text(data.VehicleModel, 100, nameof(data.VehicleModel)),
            VehicleMileageUnit = Text(data.VehicleMileageUnit, 40, nameof(data.VehicleMileageUnit)),
            AccidentCircumstances = Paragraphs(data.AccidentCircumstances, 2000, nameof(data.AccidentCircumstances)),
            ContactName = Text(data.ContactName, 300, nameof(data.ContactName)),
            ContactEmailAddress = Text(data.ContactEmailAddress, 320, nameof(data.ContactEmailAddress)),
            ContactPhoneNumber = Text(data.ContactPhoneNumber, 100, nameof(data.ContactPhoneNumber)),
            VatStatus = Text(data.VatStatus, 100, nameof(data.VatStatus)),
            InspectionAddress = Text(data.InspectionAddress, 1000, nameof(data.InspectionAddress)),
            StorageLocation = Text(data.StorageLocation, 1000, nameof(data.StorageLocation)),
            RepairerAddress = Paragraphs(data.RepairerAddress, 1000, nameof(data.RepairerAddress)),
            ClaimSourceName = Text(data.ClaimSourceName, 300, nameof(data.ClaimSourceName)),
            ClaimSourceContactName = Text(data.ClaimSourceContactName, 300, nameof(data.ClaimSourceContactName)),
            ClaimSourceContactTelephone = Text(data.ClaimSourceContactTelephone, 100, nameof(data.ClaimSourceContactTelephone)),
            ClaimSourceContactEmailAddress = Text(data.ClaimSourceContactEmailAddress, 320, nameof(data.ClaimSourceContactEmailAddress)),
            ClaimSourceCaseNote = Paragraphs(data.ClaimSourceCaseNote, 2000, nameof(data.ClaimSourceCaseNote)),
            StorageBusinessName = Text(data.StorageBusinessName, 300, nameof(data.StorageBusinessName)),
            StorageBusinessContactName = Text(data.StorageBusinessContactName, 300, nameof(data.StorageBusinessContactName)),
            StorageBusinessContactTelephone = Text(data.StorageBusinessContactTelephone, 100, nameof(data.StorageBusinessContactTelephone)),
            StorageBusinessContactEmailAddress = Text(data.StorageBusinessContactEmailAddress, 320, nameof(data.StorageBusinessContactEmailAddress)),
            VehicleMileageDisplayUnit = OdometerUnit(data.VehicleMileageDisplayUnit),
            InspectionLocationSourceLabel = Text(data.InspectionLocationSourceLabel, 300, nameof(data.InspectionLocationSourceLabel)),
            InspectionCondition = Text(data.InspectionCondition, 300, nameof(data.InspectionCondition)),
            InspectionContactName = Text(data.InspectionContactName, 300, nameof(data.InspectionContactName)),
            InspectionContactTelephone = Text(data.InspectionContactTelephone, 100, nameof(data.InspectionContactTelephone)),
            InspectionContactEmailAddress = Text(data.InspectionContactEmailAddress, 320, nameof(data.InspectionContactEmailAddress)),
            InspectionNotes = Paragraphs(data.InspectionNotes, 2000, nameof(data.InspectionNotes))
        };

        if (normalized.VehicleMileage.HasValue != (normalized.VehicleMileageUnit is not null))
        {
            throw new InvalidOperationException(
                "Vehicle mileage and mileage unit must be saved together.");
        }

        ValidateEnum(normalized.InspectionAddressTreatment, nameof(data.InspectionAddressTreatment));
        ValidateEnum(normalized.InspectionLocationChoice, nameof(data.InspectionLocationChoice));
        ValidateEnum(normalized.InspectionLocationSource, nameof(data.InspectionLocationSource));
        RequireTogether(
            normalized.ClaimSourceId.HasValue,
            normalized.ClaimSourceVersion.HasValue,
            "A claim source identifier and its version must be saved together.");
        RequireTogether(
            normalized.StorageBusinessId.HasValue,
            normalized.StorageBusinessVersion.HasValue,
            "A storage business identifier and its version must be saved together.");
        RequireTogether(
            normalized.InspectionLocationSourceId.HasValue,
            normalized.InspectionLocationSourceVersion.HasValue,
            "An inspection location source identifier and its version must be saved together.");
        if (normalized.InspectionLocationSourceId.HasValue
            && normalized.InspectionLocationSource is null)
        {
            throw new InvalidOperationException(
                "An inspection location source identifier requires the source kind it came from.");
        }

        if (normalized.ClaimSourceId == Guid.Empty
            || normalized.StorageBusinessId == Guid.Empty
            || normalized.InspectionLocationSourceId == Guid.Empty)
        {
            throw new ArgumentException(
                "A recorded source identifier cannot be empty.",
                nameof(data));
        }

        ValidateInspection(normalized);
        return normalized;
    }

    private static void RequireTogether(bool left, bool right, string message)
    {
        if (left != right)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void ValidateEnum<T>(T? value, string parameterName)
        where T : struct, Enum
    {
        if (value is { } present && !Enum.IsDefined(present))
        {
            throw new ArgumentOutOfRangeException(parameterName, "The value is invalid.");
        }
    }

    /// <summary>
    /// The canonical spelling of a recorded odometer unit. The stored original
    /// keeps whatever unit it was read in; the display unit is one of the two
    /// the record converts between, so an unrecognized one fails closed rather
    /// than becoming a silent third unit.
    /// </summary>
    private static string? OdometerUnit(string? value)
    {
        var normalized = Text(value, 40, nameof(CaseEditableData.VehicleMileageDisplayUnit));
        if (normalized is null)
        {
            return null;
        }

        return CaseOdometer.TryParseUnit(normalized, out var unit)
            ? CaseOdometer.Format(unit)
            : throw new ArgumentException(
                "The odometer display unit must be miles or kilometres.",
                nameof(value));
    }

    private static void ValidateInspection(CaseEditableData data)
    {
        if (data.InspectionMode is null && data.InspectionAddress is not null
            || data.InspectionMode is not null && data.InspectionAddress is null)
        {
            throw new InvalidOperationException(
                "A confirmed inspection address and inspection mode must be saved together.");
        }

        if (data.InspectionMode == CaseInspectionMode.ImageBasedAssessment
            && !string.Equals(
                data.InspectionAddress,
                Ext18InspectionAddressPolicy.ImageBasedAssessment,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Image Based Assessment requires the exact accepted instruction value.");
        }

        if (data.InspectionMode == CaseInspectionMode.PhysicalAddress
            && string.Equals(
                data.InspectionAddress,
                Ext18InspectionAddressPolicy.ImageBasedAssessment,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The Image Based Assessment value cannot be saved as a physical address.");
        }
    }

    private static string? Registration(string? value)
    {
        var normalized = Text(value, 20, nameof(CaseEditableData.VehicleRegistration))?
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        if (normalized is not null && normalized.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException(
                "The vehicle registration can contain only letters, digits and spaces.",
                nameof(value));
        }

        return normalized;
    }

    /// <summary>
    /// The accident circumstances are the one case text field that keeps its
    /// line structure. Every other field is a single line, so <see cref="Text"/>
    /// flattens it; the circumstances carry a labelled damage-area block below
    /// the prose, separated by a blank line, and EVA is sent that shape
    /// verbatim (ENG-015). Within a line whitespace still collapses, and runs
    /// of blank lines collapse to one, so the value cannot carry the reader's
    /// layout noise.
    /// </summary>
    private static string? Paragraphs(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => string.Join(
                ' ',
                line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .ToList();

        var normalized = new List<string>(lines.Count);
        foreach (var line in lines)
        {
            if (line.Length == 0 && (normalized.Count == 0 || normalized[^1].Length == 0))
            {
                continue;
            }

            normalized.Add(line);
        }

        while (normalized.Count > 0 && normalized[^1].Length == 0)
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        return Bounded(string.Join('\n', normalized), maximumLength, parameterName);
    }

    private static string? Text(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Bounded(
            string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)),
            maximumLength,
            parameterName);
    }

    /// <summary>The one length rule every normalized case text field obeys.</summary>
    private static string? Bounded(string value, int maximumLength, string parameterName)
    {
        if (value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return value.Length == 0 ? null : value;
    }

    private static void ValidateDate(DateOnly? value, string parameterName)
    {
        if (value == DateOnly.MinValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, "A persisted date is required.");
        }
    }
}
