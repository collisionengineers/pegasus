namespace Pegasus.Core.Cases;

/// <summary>
/// Which of the report's six mileage-source codes describes the mileage a case
/// currently stands on.
/// </summary>
/// <remarks>
/// The issued report states where the odometer figure came from, so the code is
/// not a second thing to type: the case already records the provenance of its
/// mileage, and reading that provenance is the only way the sentence and the
/// figure can never disagree. The six codes are the renderer's contract, so
/// every provenance resolves to one of them and nothing else is returned.
/// Staff pick no source: a figure staff typed is Custom on the Case page and
/// prints the online data sentence (operator, 12 October 2026).
/// </remarks>
public static class CaseVehicleMileageSourcePolicy
{
    public const string ToBeConfirmed = "tbc";
    public const string OnlineData = "online_data";
    public const string Principal = "principal";

    /// <summary>The code for a case whose mileage carries <paramref name="provenance"/>.</summary>
    public static string Resolve(CaseDataSourceKind? provenance, bool hasMileage)
    {
        if (!hasMileage)
        {
            return ToBeConfirmed;
        }

        return provenance switch
        {
            CaseDataSourceKind.VehicleLookup => OnlineData,
            // Staff typed the figure: Custom, printed as the online data sentence.
            CaseDataSourceKind.StaffCorrection or null => OnlineData,
            // Everything else reached the case through the instructing
            // Principal's own paperwork: the instruction itself, the mail route
            // that carried it, the acceptance that read it, or a Principal API.
            _ => Principal
        };
    }
}
