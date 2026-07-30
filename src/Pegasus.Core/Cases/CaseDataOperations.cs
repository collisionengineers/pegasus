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
        CaseWorkflowConfiguration configuration)
    {
        CaseDataPolicy.ValidateCompleteness(completeness);
        return EvaluateAcceptanceCommand(completeness, configuration);
    }

    internal static CaseCompletenessEvaluation EvaluateAcceptanceCommand(
        CaseCompleteness completeness,
        CaseWorkflowConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(completeness);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.PolicyKey);
        if (configuration.PolicyKey.Length > 100 || configuration.PolicyVersion < 1)
        {
            throw new InvalidOperationException(
                "The current case-workflow policy identity is invalid.");
        }

        var satisfiesPolicy =
            (!configuration.RequireCompleteInstructionsBeforeEngineerAssignment
                || completeness.InstructionComplete)
            && (!configuration.RequireCompleteImagesBeforeEngineerAssignment
                || completeness.ImagesComplete)
            && (!configuration.RequireStaffInstructionReviewBeforeEngineerAssignment
                || completeness.InstructionConfirmedByStaff)
            && (!configuration.RequireStaffImageReviewBeforeEngineerAssignment
                || completeness.ImagesConfirmedByStaff);

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

    public static void ValidateCompleteness(CaseCompleteness completeness)
    {
        ArgumentNullException.ThrowIfNull(completeness);
        if (completeness.InstructionConfirmedByStaff && !completeness.InstructionComplete)
        {
            throw new InvalidOperationException(
                "Instructions cannot be confirmed while instruction evidence is incomplete.");
        }

        if (completeness.ImagesConfirmedByStaff && !completeness.ImagesComplete)
        {
            throw new InvalidOperationException(
                "Images cannot be confirmed while image evidence is incomplete.");
        }
    }

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
            ClaimNumber = Text(data.ClaimNumber, 100, nameof(data.ClaimNumber)),
            VehicleRegistration = Registration(data.VehicleRegistration),
            VehicleMake = Text(data.VehicleMake, 100, nameof(data.VehicleMake)),
            VehicleModel = Text(data.VehicleModel, 100, nameof(data.VehicleModel)),
            VehicleMileageUnit = Text(data.VehicleMileageUnit, 40, nameof(data.VehicleMileageUnit)),
            AccidentCircumstances = Text(data.AccidentCircumstances, 2000, nameof(data.AccidentCircumstances)),
            ContactName = Text(data.ContactName, 300, nameof(data.ContactName)),
            ContactEmailAddress = Text(data.ContactEmailAddress, 320, nameof(data.ContactEmailAddress)),
            ContactPhoneNumber = Text(data.ContactPhoneNumber, 100, nameof(data.ContactPhoneNumber)),
            VatStatus = Text(data.VatStatus, 100, nameof(data.VatStatus)),
            InspectionAddress = Text(data.InspectionAddress, 1000, nameof(data.InspectionAddress))
        };

        ValidateInspection(normalized);
        return normalized;
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

    private static string? Text(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static void ValidateDate(DateOnly? value, string parameterName)
    {
        if (value == DateOnly.MinValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, "A persisted date is required.");
        }
    }
}
