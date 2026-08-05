namespace Pegasus.Core.Intake;

/// <summary>
/// What an instruction draft still needs before it can become a case.
/// </summary>
/// <remarks>
/// This rule decided the decision a draft correction produces, and it lived
/// as a private predicate inside the EF mutation store: business policy behind
/// an Infrastructure door, where no screen could ask it and no Core test could
/// see it. A create screen has to tell an operator which field is missing
/// <em>before</em> it writes anything — otherwise the correction lands, the
/// same rule fails, and the item is blocked with no warning — so the rule
/// moves to Core and both callers use it.
///
/// The names are the operator's, not the draft record's: they match the
/// extraction review field labels an operator already reads on the item.
/// </remarks>
public static class InstructionDraftCompleteness
{
    /// <summary>
    /// The required fields this draft has not answered, in operator words and
    /// in the order they are asked for. An empty list means the draft is
    /// complete.
    /// </summary>
    public static IReadOnlyList<string> MissingFieldNames(InstructionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var missing = new List<string>(10);
        if (string.IsNullOrWhiteSpace(draft.ClaimantName))
        {
            missing.Add("Claimant name");
        }
        if (string.IsNullOrWhiteSpace(draft.ClaimNumber))
        {
            missing.Add("Claim number");
        }
        if (string.IsNullOrWhiteSpace(draft.VehicleRegistration))
        {
            missing.Add("Vehicle registration");
        }
        if (string.IsNullOrWhiteSpace(draft.VehicleMake))
        {
            missing.Add("Vehicle make");
        }
        if (string.IsNullOrWhiteSpace(draft.VehicleModel))
        {
            missing.Add("Vehicle model");
        }
        if (draft.VehicleMileage is null)
        {
            missing.Add("Vehicle mileage");
        }
        if (string.IsNullOrWhiteSpace(draft.AccidentCircumstances))
        {
            missing.Add("Accident circumstances");
        }
        if (draft.DateOfIncident is null)
        {
            missing.Add("Date of incident");
        }
        if (draft.InstructionDate is null)
        {
            missing.Add("Instruction date");
        }
        if (string.IsNullOrWhiteSpace(draft.InspectionAddress))
        {
            missing.Add("Inspection address");
        }

        return missing;
    }

    /// <summary>
    /// Whether every required instruction field carries a value. Nothing about
    /// where the value came from: a draft keyed entirely by hand is as complete
    /// as one extracted from a definitive instruction.
    /// </summary>
    public static bool IsComplete(InstructionDraft draft) =>
        MissingFieldNames(draft).Count == 0;
}
