using System.Globalization;

namespace Pegasus.Core.Cases;

/// <summary>
/// The rules the Case page's save and <c>pegasus_case_update_details</c> both
/// apply when they rebuild a Case's sections from its current values with the
/// caller's changes over them (ADR-0064, R1042). Each caller reads what a rule
/// asks for itself, in its own order, and keeps its own reading of what the
/// caller named: the page's posted and unposted fields, the tool's omitted
/// value, empty string and empty identifier.
/// </summary>
public static class CaseDetailsSaveRules
{
    /// <summary>
    /// Whether the save names the directory Repairer the Case is already
    /// linked to: the link is kept with the copy it has, whether or not the
    /// record is still active, and is not linked again (R1022).
    /// </summary>
    public static bool KeepsRepairerLink(CaseWorkspaceRepairer? held, Guid? named) =>
        named is { } id && id == held?.DirectoryOrganizationId;

    /// <summary>
    /// The directory Repairer the save must read to link, or null when the
    /// save names none or names the link the Case already holds.
    /// </summary>
    public static Guid? RepairerToLink(CaseWorkspaceRepairer? held, Guid? named) =>
        named is { } id && !KeepsRepairerLink(held, named) ? id : null;

    /// <summary>
    /// The Case's Repairer after the save: the directory record just read, as
    /// it is linked (its identity, version and own name, FRD-04); otherwise
    /// the link the Case holds, or none when the save removes it, with the
    /// Case's own name for the repairer.
    /// </summary>
    public static CaseWorkspaceRepairer Repairer(
        CaseWorkspaceRepairer? held,
        ContactChoice? linked,
        bool unlinked,
        string? name) =>
        linked is not null
            ? new CaseWorkspaceRepairer(linked.OrganizationId, linked.Version, linked.Name)
            : new CaseWorkspaceRepairer(
                unlinked ? null : held?.DirectoryOrganizationId,
                unlinked ? null : held?.DirectoryOrganizationVersion,
                name);

    /// <summary>
    /// The directory Claim source the save must read and copy, or null when
    /// it names none or names the record the Case's snapshot already copies,
    /// which keeps that snapshot.
    /// </summary>
    public static Guid? ClaimSourceToCopy(CaseWorkspaceClaimSource? held, Guid? named) =>
        named is { } id && id != held?.ClaimSourceId ? id : null;

    /// <summary>A directory Claim source copied onto the Case as its snapshot, with its contact.</summary>
    public static CaseWorkspaceClaimSource ClaimSourceCopy(ContactChoice chosen)
    {
        ArgumentNullException.ThrowIfNull(chosen);
        return new(
            chosen.OrganizationId, chosen.Version, chosen.Name,
            chosen.ContactPerson, chosen.Telephone, chosen.Email);
    }

    /// <summary>
    /// How the report treats the inspection address: the mode the caller
    /// named; else the Case's recorded treatment while the address is not
    /// named; else the treatment the address itself implies.
    /// </summary>
    public static CaseReportAddressTreatment InspectionTreatment(
        CaseInspectionMode? namedMode,
        bool addressNamed,
        CaseReportAddressTreatment? held,
        string? address) =>
        namedMode is not null
            ? TreatmentOf(namedMode)
            : !addressNamed && held is { } recorded
                ? recorded
                : TreatmentOf(CaseDataPolicy.InferInspectionMode(address));

    /// <summary>The report's address treatment for an inspection mode; none is Undetermined.</summary>
    public static CaseReportAddressTreatment TreatmentOf(CaseInspectionMode? mode) => mode switch
    {
        CaseInspectionMode.ImageBasedAssessment => CaseReportAddressTreatment.ImageBasedAssessment,
        CaseInspectionMode.PhysicalAddress => CaseReportAddressTreatment.PhysicalVehicleLocation,
        _ => CaseReportAddressTreatment.Undetermined
    };

    /// <summary>
    /// The unit saved with a mileage: none without a mileage, and miles for a
    /// mileage saved without a unit.
    /// </summary>
    public static CaseOdometerUnit? MileageUnit(long? mileage, CaseOdometerUnit? unit) =>
        mileage is null ? null : unit ?? CaseOdometerUnit.Miles;

    /// <summary>
    /// The report date the Report section carries back with its Sign-off
    /// Engineer (R1019) when the caller does not change it: the recorded
    /// yyyy-MM-dd value, or none.
    /// </summary>
    public static DateOnly? RecordedReportDate(string? recorded) =>
        DateOnly.TryParseExact(recorded, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
