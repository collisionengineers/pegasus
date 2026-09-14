using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One personal notification (Work Centre D10). The row is what the bell reads;
/// nothing sends it anywhere. Rows past the retention are purged by the Worker
/// and filtered out on read before that.
/// </summary>
internal sealed class StaffNotificationEntity
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public Guid? CaseId { get; set; }
    public required string Reference { get; set; }
    public string? Registration { get; set; }
    public required string Cause { get; set; }
    public required string Route { get; set; }
    public string? ActorSubjectId { get; set; }
    public DateTimeOffset RaisedAtUtc { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
}

internal static class StaffNotificationModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<StaffNotificationEntity>(entity =>
        {
            entity.ToTable("StaffNotifications", table => table.HasCheckConstraint(
                "CK_StaffNotifications_Cause",
                "[Cause] IN ('AiDraftReady', 'CaseAssigned', 'EditedByOther', 'EmailReceived', 'QueryReceived')"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Reference).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Registration).HasMaxLength(20);
            entity.Property(item => item.Cause).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Route).HasMaxLength(400).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200);
            // The bell reads one person's rows newest first and counts their unread ones.
            entity.HasIndex(item => new { item.StaffId, item.RaisedAtUtc }).IsDescending(false, true);
            entity.HasIndex(item => item.RaisedAtUtc);
            entity.HasOne<CaseEntity>().WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
