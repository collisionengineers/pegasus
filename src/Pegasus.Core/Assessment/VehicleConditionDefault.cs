namespace Pegasus.Core.Assessment;

/// <summary>
/// The pre-incident condition every Case starts with (operator, 2 November
/// 2026; R574): Good, the way a repair spec stands on Registered for its
/// repairer VAT status. There is no Not recorded choice; staff pick another
/// condition when the vehicle's differs.
/// </summary>
public static class VehicleConditionDefault
{
    /// <summary>The <see cref="AssessmentVocabulary.VehicleCondition"/> code a new Case records.</summary>
    public const string Code = "good";

    /// <summary>
    /// The Automation actor the default records, so the Case page can show it
    /// untagged, as it would a value staff picked.
    /// </summary>
    public const string RecorderId = "vehicle-condition-default";
}
