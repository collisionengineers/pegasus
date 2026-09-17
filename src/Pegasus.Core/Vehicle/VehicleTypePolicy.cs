namespace Pegasus.Core.Vehicle;

/// <summary>
/// Derives the Case vehicle-type vocabulary code from DVLA's vehicle
/// classification signals.
/// </summary>
public static class VehicleTypePolicy
{
    public static string? Classify(VehicleDetails? vehicle)
    {
        if (vehicle is null)
        {
            return null;
        }

        var typeApproval = Normalize(vehicle.TypeApproval);
        var classifiedByApproval = typeApproval switch
        {
            "M1" => "car",
            "M2" or "M3" => "other",
            "N1" => "van",
            "N2" or "N3" => "other",
            "L1" or "L1E" or "L2" or "L2E" => "scooter",
            "L3" or "L3E" or "L4" or "L4E" or "L5" or "L5E"
                or "L6" or "L6E" or "L7" or "L7E" => "motorcycle",
            "O1" or "O2" or "O3" or "O4" => "trailer",
            _ when typeApproval.StartsWith('T')
                || typeApproval.StartsWith('C')
                || typeApproval.StartsWith('R')
                || typeApproval.StartsWith('S') => "other",
            _ => null
        };
        if (classifiedByApproval is not null)
        {
            return classifiedByApproval;
        }

        var wheelplan = Normalize(vehicle.Wheelplan);
        // VES writes "2 WHEEL"; Normalize drops spaces and hyphens alike.
        if (wheelplan.Contains("2WHEEL", StringComparison.Ordinal)
            || wheelplan.Contains("3WHEEL", StringComparison.Ordinal))
        {
            return "motorcycle";
        }
        if (wheelplan.Contains("ARTIC", StringComparison.Ordinal)
            || wheelplan.Contains("MULTIAXLE", StringComparison.Ordinal))
        {
            return "other";
        }
        if (!wheelplan.Contains("RIGIDBODY", StringComparison.Ordinal))
        {
            return null;
        }

        return vehicle.RevenueWeightKg switch
        {
            null or <= 0 => "car",
            <= 3_500 => "van",
            _ => "other"
        };
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant()
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal);
}
