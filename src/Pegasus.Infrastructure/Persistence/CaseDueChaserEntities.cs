namespace Pegasus.Infrastructure.Persistence;

internal sealed class CaseDueChaserEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseDueWorkEntity DueWork { get; set; } = null!;
    public DateTimeOffset ScheduledAtUtc { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public DateTimeOffset NextChaseAtUtc { get; set; }
    public required string CopyableText { get; set; }
    public Guid? RequestLinkReference { get; set; }
    public RequestUploadLinkEntity? RequestLink { get; set; }
    public string? RequestLinkPurpose { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
    public long BeforeDueWorkVersion { get; set; }
    public long AfterDueWorkVersion { get; set; }
}
