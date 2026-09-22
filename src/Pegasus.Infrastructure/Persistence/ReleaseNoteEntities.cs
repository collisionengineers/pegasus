using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One release note (FRD-12 What's new; FRD-17 Release notes). Written and
/// published by an Administrator in the application; a published row never
/// changes again.
/// </summary>
internal sealed class ReleaseNoteEntity
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public required string Status { get; set; }
    public string? Version { get; set; }
    public string? SourceSha { get; set; }
    public Guid CreatedByStaffId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid? PublishedByStaffId { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public long RowVersion { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
}

/// <summary>One person's acknowledgement of one published note: the What's new dialog is shown until this row exists.</summary>
internal sealed class ReleaseNoteAcknowledgementEntity
{
    public Guid StaffId { get; set; }
    public Guid ReleaseNoteId { get; set; }
    public DateTimeOffset AcknowledgedAtUtc { get; set; }
}

internal static class ReleaseNoteModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<ReleaseNoteEntity>(entity =>
        {
            entity.ToTable("ReleaseNotes", table => table.HasCheckConstraint(
                "CK_ReleaseNotes_Status",
                "[Status] IN ('Draft', 'Published')"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Body).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Version).HasMaxLength(64);
            entity.Property(item => item.SourceSha).HasMaxLength(40);
            entity.Property(item => item.RowVersion).IsConcurrencyToken();
            entity.Property(item => item.OperationKey).HasMaxLength(32).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            // The shell reads the newest published note; the Administrator lists by last change.
            entity.HasIndex(item => new { item.Status, item.PublishedAtUtc }).IsDescending(false, true);
            entity.HasIndex(item => item.UpdatedAtUtc);
        });

        builder.Entity<ReleaseNoteAcknowledgementEntity>(entity =>
        {
            entity.ToTable("ReleaseNoteAcknowledgements");
            entity.HasKey(item => new { item.StaffId, item.ReleaseNoteId });
            entity.HasOne<ReleaseNoteEntity>().WithMany().HasForeignKey(item => item.ReleaseNoteId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
