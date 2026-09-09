namespace Pegasus.Infrastructure.Persistence;

internal sealed class AutomaticEvaReviewSubmissionEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public long WorkflowVersion { get; set; }
    public required string OperationKey { get; set; }
    public required string State { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public string? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}
