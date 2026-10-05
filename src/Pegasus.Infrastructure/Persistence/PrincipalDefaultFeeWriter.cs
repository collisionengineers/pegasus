using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Gives a new Case its Principal's default fee as its agreed fee, inside the
/// creating transaction (<see cref="PrincipalDefaultFeePolicy"/>). The value
/// lands as <see cref="PrincipalDefaultFeePolicy.RecorderId"/>, so the Fee
/// pane tags it Principal until staff change it.
/// </summary>
internal static class PrincipalDefaultFeeWriter
{
    /// <summary>
    /// Writes the fee on the new Case's primary work, which holds no
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
    }
}
