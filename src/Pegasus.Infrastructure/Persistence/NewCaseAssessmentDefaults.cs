using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The assessment values a new Case starts with, written inside the creating
/// transaction: its Principal's default fee as its agreed fee
/// (<see cref="PrincipalDefaultFeePolicy"/>), which the Fee pane tags
/// Principal until staff change it, and a Good pre-incident condition
/// (<see cref="VehicleConditionDefault"/>), shown untagged.
/// </summary>
internal static class NewCaseAssessmentDefaults
{
    /// <summary>
    /// Writes the defaults on the new Case's primary work, which holds no
    /// assessment field yet.
    /// </summary>
    public static void Apply(
        PegasusDbContext context,
        Guid caseId,
        PrincipalEntity principal,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(principal);
        AssessmentFieldWriter.Write(
            context,
            caseId,
            existing: null,
            AssessmentVocabulary.AgreedFee,
            PrincipalDefaultFeePolicy.AgreedFeeValue(principal.DefaultFee),
            ActorKind.Automation,
            PrincipalDefaultFeePolicy.RecorderId,
            now);
        AssessmentFieldWriter.Write(
            context,
            caseId,
            existing: null,
            AssessmentVocabulary.VehicleCondition,
            VehicleConditionDefault.Code,
            ActorKind.Automation,
            VehicleConditionDefault.RecorderId,
            now);
    }
}
