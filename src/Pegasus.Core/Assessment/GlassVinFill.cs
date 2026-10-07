using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

/// <summary>
/// Where a VIN Glass's names lands on a Case: the stocked vehicle a Get
/// valuation creates and every Glass's estimate (its XML export or its
/// calculation PDF) name the vehicle's VIN. It fills the work's VIN only when
/// the work holds none, whoever would have recorded one (operator, 7 October
/// 2026); a VIN already on the Case is never replaced.
/// </summary>
public static class GlassVinFillPolicy
{
    /// <summary>The Automation actor a filled VIN is recorded as.</summary>
    public const string RecorderId = "glass-vin";

    /// <summary>The VIN fills only where the work holds none.</summary>
    public static bool Fills(string? existing) => string.IsNullOrWhiteSpace(existing);

    /// <summary>
    /// Glass's VIN in the vocabulary's canonical form, or null when it names
    /// none or one the vocabulary cannot hold.
    /// </summary>
    public static string? Carried(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return AssessmentPolicy.NormalizeFieldValue(AssessmentVocabulary.VehicleVin, raw);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

/// <summary>
/// Records the VIN a Glass's valuation named on the work it valued, as system
/// work that keeps whoever is editing in their session. A VIN that cannot be
/// recorded never holds back the valuation it came with.
/// </summary>
public interface IFillGlassVin
{
    Task FillAsync(
        ActionActor actor,
        Guid caseId,
        CaseWorkSelector work,
        string vin,
        CancellationToken cancellationToken);
}
