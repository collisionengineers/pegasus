using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class CaseDueChaserModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<CaseDueChaserEntity>(entity =>
        {
            entity.ToTable("CaseDueChasers", table =>
            {
                table.HasCheckConstraint(
                    "CK_CaseDueChasers_Versions",
                    "[BeforeDueWorkVersion] >= 0 AND [AfterDueWorkVersion] = [BeforeDueWorkVersion] + 1");
                table.HasCheckConstraint(
                    "CK_CaseDueChasers_RequestLink",
                    "([RequestLinkReference] IS NULL AND [RequestLinkPurpose] IS NULL) OR ([RequestLinkReference] IS NOT NULL AND [RequestLinkPurpose] = 'missing-material-upload')");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CopyableText).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.RequestLinkPurpose).HasMaxLength(100);
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.ScheduledAtUtc }).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.GeneratedAtUtc });
            entity.HasOne(item => item.DueWork)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.RequestLink)
                .WithMany()
                .HasForeignKey(item => item.RequestLinkReference)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
