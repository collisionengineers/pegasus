namespace Pegasus.Core.Cases;

/// <summary>
/// The Principal-determined inspection mode (ADR-0018). Each Principal persists
/// an inspection-mode setting; an always-image-based Principal has the exact
/// value "Image Based Assessment" autofilled at Inspect at and the inspection
/// address at Case creation with this policy's provenance (R1078), and staff
/// may change Inspect at on the specific Case as an ordinary edit (R1079).
/// </summary>
public static class PrincipalInspectionModePolicy
{
    public const string PolicyKey = "principal-inspection-mode";
    public const int PolicyVersion = 1;

    public const string PhysicalAddressCode = "physical_address";
    public const string ImageBasedAssessmentCode = "image_based_assessment";

    public static string ToCode(CaseInspectionMode mode) => mode switch
    {
        CaseInspectionMode.PhysicalAddress => PhysicalAddressCode,
        CaseInspectionMode.ImageBasedAssessment => ImageBasedAssessmentCode,
        _ => throw new ArgumentOutOfRangeException(
            nameof(mode),
            "The Principal inspection mode is invalid.")
    };

    public static CaseInspectionMode Parse(string code) => code switch
    {
        PhysicalAddressCode => CaseInspectionMode.PhysicalAddress,
        ImageBasedAssessmentCode => CaseInspectionMode.ImageBasedAssessment,
        _ => throw new InvalidDataException(
            $"Unknown Principal inspection-mode code '{code}'.")
    };
}

/// <summary>
/// Reads the persisted inspection-mode setting of an active Principal.
/// Returns null when no active Principal carries the code.
/// </summary>
public interface IPrincipalInspectionModeStore
{
    Task<CaseInspectionMode?> GetForPrincipalAsync(
        string principalCode,
        CancellationToken cancellationToken);
}
