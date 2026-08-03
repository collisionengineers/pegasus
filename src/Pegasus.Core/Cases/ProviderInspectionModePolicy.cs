namespace Pegasus.Core.Cases;

/// <summary>
/// The provider-determined inspection mode (ADR-0017). Each Principal persists
/// an inspection-mode setting; an always-image-based Principal has the exact
/// value "Image Based Assessment" autofilled at Case creation with this
/// policy's provenance, and staff may override it on the specific Case with an
/// attributed reason.
/// </summary>
public static class ProviderInspectionModePolicy
{
    public const string PolicyKey = "provider-inspection-mode";
    public const int PolicyVersion = 1;

    public const string PhysicalAddressCode = "physical_address";
    public const string ImageBasedAssessmentCode = "image_based_assessment";

    public static string ToCode(CaseInspectionMode mode) => mode switch
    {
        CaseInspectionMode.PhysicalAddress => PhysicalAddressCode,
        CaseInspectionMode.ImageBasedAssessment => ImageBasedAssessmentCode,
        _ => throw new ArgumentOutOfRangeException(
            nameof(mode),
            "The provider inspection mode is invalid.")
    };

    public static CaseInspectionMode Parse(string code) => code switch
    {
        PhysicalAddressCode => CaseInspectionMode.PhysicalAddress,
        ImageBasedAssessmentCode => CaseInspectionMode.ImageBasedAssessment,
        _ => throw new InvalidDataException(
            $"Unknown provider inspection-mode code '{code}'.")
    };
}

/// <summary>
/// Reads the persisted inspection-mode setting of an active Principal.
/// Returns null when no active Principal carries the code.
/// </summary>
public interface IProviderInspectionModeStore
{
    Task<CaseInspectionMode?> GetForPrincipalAsync(
        string principalCode,
        CancellationToken cancellationToken);
}
