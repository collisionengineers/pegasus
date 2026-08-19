namespace Pegasus.Core.ImageIntake;

/// <summary>
/// The one decision table used when a manual upload contains more than one
/// image. Recognition is evaluated across the whole group, so a close-up that
/// has no plate cannot detach itself from an overview that identifies the
/// vehicle.
/// </summary>
public enum ImageIntakeGroupRoutingDecision
{
    WaitingForMembers,
    WaitingForRecognition,
    AssociateExistingCase,
    HandOffToImageIntake,
    RouteToUnidentified,
    TechnicalFailure
}

public sealed record ImageIntakeGroupMemberRecognition(
    Guid ReceiptId,
    bool IsTerminal,
    VrmRecognitionOutcomeKind Outcome,
    string? NormalizedRegistration,
    double? Confidence,
    string? FailureCode = null);

public sealed record ImageIntakeGroupRoutingResult(
    ImageIntakeGroupRoutingDecision Decision,
    string? NormalizedRegistration,
    string ReasonCode);

public static class ImageIntakeGroupRoutingPolicy
{
    public static ImageIntakeGroupRoutingResult Evaluate(
        IReadOnlyList<ImageIntakeGroupMemberRecognition> members,
        int expectedMemberCount,
        int eligibleCaseCount)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedMemberCount, 1);

        if (members.Count < expectedMemberCount)
        {
            return new(
                ImageIntakeGroupRoutingDecision.WaitingForMembers,
                null,
                "group_members_incomplete");
        }

        if (members.Any(member => !member.IsTerminal))
        {
            return new(
                ImageIntakeGroupRoutingDecision.WaitingForRecognition,
                null,
                "group_recognition_incomplete");
        }

        if (members.Any(member => member.Outcome is VrmRecognitionOutcomeKind.TechnicalFailure
                or VrmRecognitionOutcomeKind.Unavailable))
        {
            return new(
                ImageIntakeGroupRoutingDecision.TechnicalFailure,
                null,
                "group_recognition_failure");
        }

        var registrations = members
            .Where(member => member.Outcome == VrmRecognitionOutcomeKind.Suggested
                && member.NormalizedRegistration is not null
                && member.Confidence >= VrmRecognitionProvisionalBar.MinimumAutomaticConfidence)
            .Select(member => member.NormalizedRegistration!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (registrations.Length == 1 && eligibleCaseCount == 1)
        {
            return new(
                ImageIntakeGroupRoutingDecision.AssociateExistingCase,
                registrations[0],
                "group_single_vrm_single_eligible_case");
        }

        var reason = registrations.Length switch
        {
            0 => "group_no_accepted_vrm",
            1 when eligibleCaseCount == 0 => "group_vrm_no_eligible_case",
            1 => "group_vrm_eligible_case_ambiguous",
            _ => "conflicting_vrms"
        };
        var decision = registrations.Length == 1
            ? ImageIntakeGroupRoutingDecision.HandOffToImageIntake
            : ImageIntakeGroupRoutingDecision.RouteToUnidentified;
        return new(
            decision,
            registrations.Length == 1 ? registrations[0] : null,
            reason);
    }
}
