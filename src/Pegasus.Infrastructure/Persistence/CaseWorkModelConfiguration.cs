using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class CaseWorkModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<CaseWorkEntity>(entity =>
        {
            entity.ToTable("CaseWorks", table =>
            {
                table.HasCheckConstraint(
                    "CK_CaseWorks_Kind",
                    "[Kind] IN (N'primary', N'audit')");
                // The primary work shares the Case's id, so every read that
                // joins a per-work row on the Case id reads the primary work.
                table.HasCheckConstraint(
                    "CK_CaseWorks_PrimaryId",
                    "[Kind] <> N'primary' OR [Id] = [CaseId]");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.Kind).HasMaxLength(20).IsRequired();
            entity.HasIndex(item => new { item.CaseId, item.Kind }).IsUnique();
            entity.HasIndex(item => item.ReportApprovalId).IsUnique();
            entity.HasIndex(item => item.ReportSentEvidenceId).IsUnique();
            entity.HasOne(item => item.Case)
                .WithMany(item => item.Works)
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReportApproval)
                .WithMany()
                .HasForeignKey(item => item.ReportApprovalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReportSentEvidence)
                .WithMany()
                .HasForeignKey(item => item.ReportSentEvidenceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
