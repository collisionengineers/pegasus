namespace Pegasus.Core.Workflow;

public sealed class DefaultCaseWorkflowConfiguration : ICaseWorkflowConfiguration
{
    private static readonly CaseWorkflowConfiguration Current = new(
        RequireCompleteInstructionsBeforeEngineerAssignment: true,
        RequireCompleteImagesBeforeEngineerAssignment: true,
        RequireStaffInstructionReviewBeforeEngineerAssignment: true,
        RequireStaffImageReviewBeforeEngineerAssignment: true,
        PolicyKey: "case-workflow",
        PolicyVersion: 1);

    public CaseWorkflowConfiguration GetCurrent() => Current;
}
