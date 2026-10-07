using System.Globalization;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake;

/// <summary>
/// One field of an <see cref="InstructionDraft"/>, named by the label an
/// operator reads on the intake item.
/// </summary>
/// <param name="Required">The draft is not complete without it.</param>
/// <param name="IdentityCritical">It says which claim the case is about, so it alone may withhold a reference.</param>
/// <param name="StaffKeyed">A staff correction states it, so the correction is written back to its review field.</param>
public sealed record InstructionDraftField(
    string Label,
    string CaseDataFieldName,
    Func<InstructionDraft, string?> Value,
    bool Required = false,
    bool IdentityCritical = false,
    bool StaffKeyed = false);

/// <summary>
/// The instruction draft's fields in one list: their labels, the case field
/// each binds to, and the rules that read them. Completeness, the review
/// fields a declaration produces, a staff correction and the case snapshot all
/// read this list rather than spelling the labels again.
/// </summary>
public static class InstructionDraftFields
{
    /// <summary>In the order an operator is asked for them.</summary>
    public static readonly IReadOnlyList<InstructionDraftField> All =
    [
        new("Claimant name", CaseDataFieldNames.ClaimantName, draft => draft.ClaimantName,
            Required: true, IdentityCritical: true, StaffKeyed: true),
        new("Claim number", CaseDataFieldNames.ClaimNumber, draft => draft.ClaimNumber,
            Required: true, IdentityCritical: true, StaffKeyed: true),
        new("Vehicle registration", CaseDataFieldNames.VehicleRegistration, draft => draft.VehicleRegistration,
            Required: true, IdentityCritical: true, StaffKeyed: true),
        new("Vehicle make", CaseDataFieldNames.VehicleMake, draft => draft.VehicleMake,
            Required: true, StaffKeyed: true),
        new("Vehicle model", CaseDataFieldNames.VehicleModel, draft => draft.VehicleModel,
            Required: true, StaffKeyed: true),
        new("Vehicle mileage", CaseDataFieldNames.VehicleMileage,
            draft => draft.VehicleMileage?.ToString(CultureInfo.InvariantCulture),
            Required: true, StaffKeyed: true),
        new("Accident circumstances", CaseDataFieldNames.AccidentCircumstances, draft => draft.AccidentCircumstances,
            Required: true, StaffKeyed: true),
        new("Date of incident", CaseDataFieldNames.IncidentDate, draft => Date(draft.DateOfIncident),
            Required: true, StaffKeyed: true),
        new("Inspection address", CaseDataFieldNames.InspectionAddress, draft => draft.InspectionAddress,
            Required: true, StaffKeyed: true),
        new("Inspection date", CaseDataFieldNames.InspectionDate, draft => Date(draft.InspectionDate),
            StaffKeyed: true),
        new("Vehicle mileage unit", CaseDataFieldNames.VehicleMileageUnit, draft => draft.VehicleMileageUnit),
        new("VAT status", CaseDataFieldNames.VatStatus, draft => draft.VatStatus),
        new("Claimant address", CaseDataFieldNames.ClaimantAddress, draft => draft.ClaimantAddress),
        new("Claimant contact number", CaseDataFieldNames.ClaimantContactNumber, draft => draft.ClaimantContactNumber),
        new("Contact name", CaseDataFieldNames.ContactName, draft => draft.FileHandlerName),
        new("Contact email", CaseDataFieldNames.ContactEmailAddress, draft => draft.FileHandlerEmailAddress),
        new("Contact phone", CaseDataFieldNames.ContactPhoneNumber, draft => draft.FileHandlerPhoneNumber)
    ];

    /// <summary>
    /// The case field each review-field label binds to: every draft field's
    /// own label, and the other labels the extraction profiles print for the
    /// same facts.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> CaseDataFieldNamesByLabel =
        All.Select(field => KeyValuePair.Create(field.Label, field.CaseDataFieldName))
            .Concat(
            [
                KeyValuePair.Create("Claim reference", CaseDataFieldNames.ClaimNumber),
                KeyValuePair.Create("Vehicle description", CaseDataFieldNames.VehicleDescription),
                KeyValuePair.Create("Vehicle make and model", CaseDataFieldNames.VehicleMake),
                KeyValuePair.Create("Incident date", CaseDataFieldNames.IncidentDate),
                KeyValuePair.Create("Claimant mobile telephone", CaseDataFieldNames.ClaimantContactNumber),
                KeyValuePair.Create("Claimant home telephone", CaseDataFieldNames.ClaimantContactNumber),
                // The repairer the instruction names. The profiles print these
                // two labels; the Case keeps the name and the address as its
                // own facts, and a directory link is a separate staff decision.
                KeyValuePair.Create("Repairer name", CaseDataFieldNames.RepairerName),
                KeyValuePair.Create("Repairer address", CaseDataFieldNames.RepairerAddress)
            ])
            .ToDictionary(StringComparer.Ordinal);

    public static string? CaseDataFieldName(string label) =>
        CaseDataFieldNamesByLabel.GetValueOrDefault(label);

    private static string? Date(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
