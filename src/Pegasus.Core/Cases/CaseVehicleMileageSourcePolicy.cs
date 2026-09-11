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
/// The staff pick survives only where staff supplied the figure — an owner, a
/// repairer or the instructing Principal told them, and only they know which.
/// </remarks>
public static class CaseVehicleMileageSourcePolicy
{
    public const string ToBeConfirmed = "tbc";
    public const string OnlineData = "online_data";
    public const string Principal = "principal";
    public const string Owner = "owner";
    public const string Repairer = "repairer";

    /// <summary>
    /// The codes a member of staff may pick from once they have entered the
    /// mileage themselves. The remaining codes are derived, never chosen.
    /// </summary>
    public static readonly IReadOnlyList<string> StaffChoices = [Owner, Repairer, Principal];

    /// <summary>
    /// The code for a case whose mileage carries <paramref name="provenance"/>.
    /// <paramref name="staffChoice"/> is the value staff recorded and is read
    /// only where staff are the source of the figure; an unrecognized or
    /// missing pick falls back to the owner, who is who staff most often ask.
    /// </summary>
    public static string Resolve(
        CaseDataSourceKind? provenance,
        bool hasMileage,
        string? staffChoice)
    {
        if (!hasMileage)
        {
            return ToBeConfirmed;
        }

        return provenance switch
        {
            CaseDataSourceKind.VehicleLookup => OnlineData,
            // Staff typed the figure, and only they know who told them.
            CaseDataSourceKind.StaffCorrection or null => StaffChoice(staffChoice),
            // Everything else reached the case through the instructing
            // Principal's own paperwork: the instruction itself, the mail route
            // that carried it, the acceptance that read it, or a provider API.
            _ => Principal
        };
    }

    private static string StaffChoice(string? staffChoice) =>
        staffChoice is { } choice && StaffChoices.Contains(choice) ? choice : Owner;
}
