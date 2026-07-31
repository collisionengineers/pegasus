namespace Pegasus.Infrastructure.Persistence;

internal sealed class WorkflowConfigurationEntity
{
    public required string Id { get; set; }
    public bool RequireCompleteInstructionsBeforeEngineerAssignment { get; set; }
    public bool RequireCompleteImagesBeforeEngineerAssignment { get; set; }
    public bool RequireStaffInstructionReviewBeforeEngineerAssignment { get; set; }
    public bool RequireStaffImageReviewBeforeEngineerAssignment { get; set; }
    public int Version { get; set; }
}

internal sealed class ApprovedMailboxEntity
{
    public Guid Id { get; set; }
    public required string Address { get; set; }
    public bool AllowInboundIntake { get; set; }
    public bool AllowSentEvidence { get; set; }
    public required string State { get; set; }
    public int Version { get; set; }
}
